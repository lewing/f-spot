using Hyena;
using FSpot.Utils;
using FSpot.Xmp;
using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Unix;

namespace FSpot.Import
{
    public enum ImportEvent {
        SourceChanged,
        PhotoScanStarted,
        PhotoScanFinished,
        ImportStarted,
        ImportFinished,
        ImportError
    }

    public class ImportController
    {
        public PhotoList Photos { get; private set; }

        public ImportController ()
        {
            Photos = new PhotoList ();
            LoadPreferences ();
        }

        ~ImportController ()
        {
            DeactivateSource (ActiveSource);
        }

#region Import Preferences

        private bool copy_files;
        private bool recurse_subdirectories;
        private bool duplicate_detect;

        public bool CopyFiles {
            get { return copy_files; }
            set { copy_files = value; SavePreferences (); }
        }

        public bool RecurseSubdirectories {
            get { return recurse_subdirectories; }
            set { recurse_subdirectories = value; SavePreferences (); RescanPhotos (); }
        }

        public bool DuplicateDetect {
            get { return duplicate_detect; }
            set { duplicate_detect = value; SavePreferences (); }
        }

        void LoadPreferences ()
        {
            copy_files = Preferences.Get<bool> (Preferences.IMPORT_COPY_FILES);
            recurse_subdirectories = Preferences.Get<bool> (Preferences.IMPORT_INCLUDE_SUBFOLDERS);
            duplicate_detect = Preferences.Get<bool> (Preferences.IMPORT_CHECK_DUPLICATES);
        }

        void SavePreferences ()
        {
            Preferences.Set(Preferences.IMPORT_COPY_FILES, copy_files);
            Preferences.Set(Preferences.IMPORT_INCLUDE_SUBFOLDERS, recurse_subdirectories);
            Preferences.Set(Preferences.IMPORT_CHECK_DUPLICATES, duplicate_detect);
        }

#endregion

#region Source Scanning

        private List<ImportSource> sources;
        public List<ImportSource> Sources {
            get {
                if (sources == null)
                    sources = ScanSources ();
                return sources;
            }
        }

        List<ImportSource> ScanSources ()
        {
		    var monitor = GLib.VolumeMonitor.Default;
            var sources = new List<ImportSource> ();
            foreach (var mount in monitor.Mounts) {
                var root = new SafeUri (mount.Root.Uri, true);
                var icon = (mount.Icon as GLib.ThemedIcon).Names [0];
                sources.Add (new FileImportSource (root, mount.Name, icon));
            }
            return sources;
        }

#endregion

#region Status Reporting

        public delegate void ImportProgressHandler (int current, int total);
        public event ImportProgressHandler ProgressUpdated;

        public delegate void ImportEventHandler (ImportEvent evnt);
        public event ImportEventHandler StatusEvent;

        void FireEvent (ImportEvent evnt)
        {
            ThreadAssist.ProxyToMain (() => {
                var h = StatusEvent;
                if (h != null)
                    h (evnt);
            });
        }

        void ReportProgress (int current, int total)
        {
            var h = ProgressUpdated;
            if (h != null)
                h (current, total);
        }

        public int PhotosImported { get; private set; }
        public Roll CreatedRoll { get; private set; }

#endregion

#region Source Switching

        private ImportSource active_source;
        public ImportSource ActiveSource {
            set {
                if (value == active_source)
                    return;
                var old_source = active_source;
                active_source = value;
                FireEvent (ImportEvent.SourceChanged);
                RescanPhotos ();
                DeactivateSource (old_source);
            }
            get {
                return active_source;
            }
        }

        void DeactivateSource (ImportSource source)
        {
            if (source == null)
                return;
            source.Deactivate ();
        }

        void RescanPhotos ()
        {
            Photos.Clear ();
            if (ActiveSource == null)
                return;

            photo_scan_running = true;
            ActiveSource.StartPhotoScan (this);
            FireEvent (ImportEvent.PhotoScanStarted);
        }

#endregion

#region Source Progress Signalling

        // These are callbacks that should be called by the sources.

        public void PhotoScanFinished ()
        {
            photo_scan_running = false;
            FireEvent (ImportEvent.PhotoScanFinished);
        }

#endregion

#region Importing

        Thread ImportThread;

        public void StartImport ()
        {
            if (ImportThread != null)
                throw new Exception ("Import already running!");

            ImportThread = ThreadAssist.Spawn (() => DoImport ());
        }

        public void CancelImport ()
        {
            import_cancelled = true;
            if (ImportThread != null)
                ImportThread.Join ();
            Cleanup ();
        }

        Stack<SafeUri> created_directories;
        List<uint> imported_photos;
        List<SafeUri> copied_files;
        PhotoStore store = App.Instance.Database.Photos;
        RollStore rolls = App.Instance.Database.Rolls;
        volatile bool photo_scan_running;
        XmpTagsImporter xmp_importer;
        volatile bool import_cancelled = false;

        void DoImport ()
        {
            while (photo_scan_running) {
                Thread.Sleep (1000); // FIXME: we can do this with a better primitive!
            }

            FireEvent (ImportEvent.ImportStarted);
            App.Instance.Database.Sync = false;
            created_directories = new Stack<SafeUri> ();
            imported_photos = new List<uint> ();
            copied_files = new List<SafeUri> ();
            xmp_importer = new XmpTagsImporter (store, App.Instance.Database.Tags);
            CreatedRoll = rolls.Create ();

            EnsureDirectory (Global.PhotoUri);

            try {
                int i = 0;
                int total = Photos.Count;
                foreach (var info in Photos.Items) {
                    if (import_cancelled) {
                        RollbackImport ();
                        return;
                    }

                    ThreadAssist.ProxyToMain (() => ReportProgress (i++, total));
                    ImportPhoto (info, CreatedRoll);
                }

                PhotosImported = imported_photos.Count;
                FinishImport ();
            } catch (Exception e) {
                RollbackImport ();
                throw e;
            } finally {
                Cleanup ();
            }
        }

        void Cleanup ()
        {
            if (imported_photos != null && imported_photos.Count == 0)
                rolls.Remove (CreatedRoll);
            imported_photos = null;
            created_directories = null;
            Photo.ResetMD5Cache ();
            DeactivateSource (ActiveSource);
            Photos.Clear ();
            System.GC.Collect ();
            App.Instance.Database.Sync = true;
        }

        void FinishImport ()
        {
            ThreadAssist.SpawnFromMain (() => {
                // Generate all thumbnails on a different thread, disposing is automatic.
                var loader = ThumbnailLoader.Default;
                foreach (var id in imported_photos) {
                    var uri = store.Get (id).DefaultVersion.Uri;
                    loader.Request (uri, ThumbnailSize.Large, 10);
                }
            });

            ImportThread = null;
            FireEvent (ImportEvent.ImportFinished);
        }

        void RollbackImport ()
        {
            // Remove photos
            foreach (var id in imported_photos) {
                store.Remove (store.Get (id));
            }

            foreach (var uri in copied_files) {
                var file = GLib.FileFactory.NewForUri (uri);
                file.Delete (null);
            }

            // Clean up directories
            while (created_directories.Count > 0) {
                var uri = created_directories.Pop ();
                var dir = GLib.FileFactory.NewForUri (uri);
                var enumerator = dir.EnumerateChildren ("standard::name", GLib.FileQueryInfoFlags.None, null);
                if (!enumerator.HasPending) {
                    dir.Delete (null);
                }
            }

            // Clean created tags
            xmp_importer.Cancel();

            // Remove created roll
		    rolls.Remove (CreatedRoll);
        }

        void ImportPhoto (IBrowsableItem item, Roll roll)
        {
            var destination = FindImportDestination (item.DefaultVersion.Uri);

            // Do duplicate detection
            if (DuplicateDetect && store.CheckForDuplicate (item.DefaultVersion.Uri, destination)) {
                return;
            }

            // Copy into photo folder.
			if (item.DefaultVersion.Uri != destination) {
				var file = GLib.FileFactory.NewForUri (item.DefaultVersion.Uri);
				var new_file = GLib.FileFactory.NewForUri (destination);
				file.Copy (new_file, GLib.FileCopyFlags.AllMetadata, null, null);
                copied_files.Add (destination);
            }

            // Import photo
            var photo = store.Create (destination,
                                      item.DefaultVersion.Uri,
                                      roll.Id);

            bool needs_commit = false;

            // Add tags
            if (attach_tags.Count > 0) {
                photo.AddTag (attach_tags);
                needs_commit = true;
            }

            // Import XMP metadata
            needs_commit |= xmp_importer.Import (photo, destination, item.DefaultVersion.Uri);

            if (needs_commit) {
                store.Commit (photo);
            }

            // FIXME: import xmp crap
            imported_photos.Add (photo.Id);
        }

        SafeUri FindImportDestination (SafeUri uri)
        {
            if (!CopyFiles)
                return uri; // Keep it at the same place

            // Find a new unique location inside the photo folder
            string name = uri.GetFilename ();
            DateTime time;
            using (FSpot.ImageFile img = FSpot.ImageFile.Create (uri)) {
                time = img.Date;
            }

            var dest_uri = Global.PhotoUri.Append (time.Year.ToString ())
                                          .Append (String.Format ("{0:D2}", time.Month))
                                          .Append (String.Format ("{0:D2}", time.Day));
            EnsureDirectory (dest_uri);

            // If the destination we'd like to use is the file itself return that
            if (dest_uri.Append (name) == uri)
                return uri;

            // Find an unused name
            int i = 1;
            var dest = dest_uri.Append (name);
            var file = GLib.FileFactory.NewForUri (dest);
            while (file.Exists) {
                var filename = uri.GetFilenameWithoutExtension ();
                var extension = uri.GetExtension ();
                dest = dest_uri.Append (String.Format ("{0}-{1}{2}", filename, i++, extension));
                file = GLib.FileFactory.NewForUri (dest);
            }

            return dest;
        }

        void EnsureDirectory (SafeUri uri)
        {
            var parts = uri.AbsolutePath.Split('/');
            SafeUri current = new SafeUri (uri.Scheme + ":///", true);
            for (int i = 0; i < parts.Length; i++) {
                current = current.Append (parts [i]);
                var file = GLib.FileFactory.NewForUri (current);
                if (!file.Exists) {
                    created_directories.Push (current);
                    Log.Debug ("Creating "+current);
                    file.MakeDirectory (null);
                }
            }
        }

#endregion

#region Tagging

        List<Tag> attach_tags = new List<Tag> ();
        TagStore tag_store = App.Instance.Database.Tags;

        // Set the tags that will be added on import.
        public void AttachTags (IEnumerable<string> tags)
        {
            App.Instance.Database.BeginTransaction ();
            var import_category = GetImportedTagsCategory ();
            foreach (var tagname in tags) {
                var tag = tag_store.GetTagByName (tagname);
                if (tag == null) {
                    tag = tag_store.CreateCategory (import_category, tagname, false) as Tag;
                    tag_store.Commit (tag);
                }
                attach_tags.Add (tag);
            }
            App.Instance.Database.CommitTransaction ();
        }

        Category GetImportedTagsCategory ()
        {
            var default_category = tag_store.GetTagByName (Catalog.GetString ("Imported Tags")) as Category;
            if (default_category == null) {
                default_category = tag_store.CreateCategory (null, Catalog.GetString ("Imported Tags"), false);
                default_category.ThemeIconName = "gtk-new";
            }
            return default_category;
        }

#endregion

    }
}
