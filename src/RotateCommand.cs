/*
 * Filters/IFilter.cs
 *
 * Author(s)
 *   Larry Ewing <lewing@novell.com>
 *
 * This is free software. See COPYING for details
 *
 */

using System;
using System.IO;

using Gtk;
using Gdk;

using FSpot;
using FSpot.Png;

namespace FSpot {
	public class RotateException : ApplicationException {
		public string path;
		
		public string Path {
			get { return path; }
		}
		
		public RotateException (string msg, string path) : base (msg) {
			this.path = path;
		}
	}

	public enum RotateDirection {
		Clockwise,
		Counterclockwise,
	}

	public class RotateOperation : Operation {
		IBrowsableItem item;
		RotateDirection direction;
		int version_index;
		bool done;

		public RotateOperation (IBrowsableItem item, RotateDirection direction)
		{
			this.item = item;
			this.direction = direction;
			version_index = 0;
			done = false;
		}

		private static void RotateCoefficients (string original_path, RotateDirection direction)
		{
			string temporary_path = original_path + ".tmp";	// FIXME make it unique
			JpegUtils.Transform (original_path, temporary_path, 
					     direction == RotateDirection.Clockwise ? JpegUtils.TransformType.Rotate90 
					     : JpegUtils.TransformType.Rotate270);
			
			Unix.Rename (temporary_path, original_path);
		}

		private static void RotateOrientation (string original_path, RotateDirection direction)
		{
			FSpot.ImageFile img = FSpot.ImageFile.Create (original_path);
			
			if (img is JpegFile) {
				FSpot.JpegFile jimg = img as FSpot.JpegFile;
				PixbufOrientation orientation = direction == RotateDirection.Clockwise
					? PixbufUtils.Rotate90 (img.Orientation)
					: PixbufUtils.Rotate270 (img.Orientation);
				
				jimg.SetOrientation (orientation);
				jimg.SaveMetaData (original_path);
			} else if (img is PngFile) {
				PngFile png = img as PngFile;
				bool supported = false;

				foreach (PngFile.Chunk c in png.Chunks) {
					PngFile.IhdrChunk ihdr = c as PngFile.IhdrChunk;
					
					if (ihdr != null && ihdr.Depth == 8)
						supported = true;
				}

				if (! supported)
					throw new RotateException ("Unable to rotate photo type", original_path);

				string backup = ImageFile.TempPath (original_path);
				using (Stream stream = File.Open (backup, FileMode.Truncate, FileAccess.Write)) {
					using (Pixbuf pixbuf = img.Load ()) {
						PixbufOrientation fake = (direction == RotateDirection.Clockwise) ? PixbufOrientation.RightTop : PixbufOrientation.LeftBottom;
						using (Pixbuf rotated = PixbufUtils.TransformOrientation (pixbuf, fake)) {
							Console.WriteLine ("fake = {0}", fake);
							img.Save (rotated, stream);
						}
					}
				}
				File.Copy (backup, original_path, true);
			} else {
				throw new RotateException ("Unable to rotate photo type", original_path);
			}
		}
		       
		private void Rotate (string original_path, RotateDirection dir)
		{
			RotateOrientation (original_path, direction);
		}
		
		public override bool Step () {
			string original_path;

			if (done)
				return false;

			if (item is Photo) {
				Photo p = (Photo) item;

				original_path = p.GetVersionPath (p.VersionIds [version_index++]);
				done = (version_index >= p.VersionIds.Length);
			} else {
				original_path = item.DefaultVersionUri.LocalPath;
				done = true;
			}

			if ((File.GetAttributes(original_path) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				throw new RotateException ("Unable to rotate readonly file", original_path);

			Rotate (original_path, direction);

			Gdk.Pixbuf thumb = FSpot.ThumbnailGenerator.Create (original_path);
			if (thumb != null)
				thumb.Dispose ();
		
			return !done;
		}
	}

	public class RotateMultiple : Operation {
		RotateDirection direction;
		IBrowsableItem [] items;
		bool rotate_data = false;
		int index;
		RotateOperation op;

		public int Index { 
			get { return index; }
		}

		public IBrowsableItem [] Items {
			get { return items; }
		}

		public RotateMultiple (IBrowsableItem [] items, RotateDirection direction)
		{
			this.direction = direction;
			this.items = items;
			index = 0;
		}
		
		public override bool Step ()
		{
			if (index >= items.Length)  
				return false;

			if (op == null) 
				op = new RotateOperation (items [index], direction);
			
			if (op.Step ())
				return true;
			else {
				index++;
				op = null;
			}

			return (index < items.Length);
		}
	}
}

public class RotateCommand {
	private Gtk.Window parent_window;

	public RotateCommand (Gtk.Window parent_window)
	{
		this.parent_window = parent_window;
	}

	public bool Execute (RotateDirection direction, IBrowsableItem [] items)
	{
		ProgressDialog progress_dialog = null;
		
		if (items.Length > 1)
			progress_dialog = new ProgressDialog (Mono.Posix.Catalog.GetString ("Rotating photos"),
							      ProgressDialog.CancelButtonType.Stop,
							      items.Length, parent_window);
		
	        RotateMultiple op = new RotateMultiple (items, direction);
		int readonly_count = 0;
		bool done = false;
		int index = -1;

		while (!done) {
			if (progress_dialog != null && index != op.Index) 
				progress_dialog.Update (String.Format (Mono.Posix.Catalog.GetString ("Rotating photo \"{0}\""), op.Items [op.Index].Name));

			try {
				done = !op.Step ();
			} catch (RotateException re) {
				if (re.Message == "Unable to rotate photo type")
					RunTypeError (re);
				else
					readonly_count ++;
			} catch (Exception e) {
				RunGenericError (e, op.Items [op.Index].DefaultVersionUri.LocalPath);
			}
		}
		
		if (progress_dialog != null)
			progress_dialog.Destroy ();
		
		if (readonly_count > 0)
			RunReadonlyError (readonly_count);
		
		return true;
	}

	private void RunReadonlyError (int readonly_count)
	{
		string notice = Mono.Posix.Catalog.GetPluralString ("Unable to rotate photo",  
								    "Unable to rotate {0} photos",  
								    readonly_count);
		
		string desc = Mono.Posix.Catalog.GetPluralString ("The photo could not be rotated because it is on a read only file system or " + 
								  "media such as a CDROM.  Please check the permissions and try again",  
								  "{0} photos could not be rotated because they are on a read only file system " + 
								  "or media such as a CDROM.  Please check the permissions and try again",  readonly_count);
		
		HigMessageDialog md = new HigMessageDialog (parent_window, 
							    DialogFlags.DestroyWithParent,
							    MessageType.Error,
							    ButtonsType.Close,
							    notice, 
							    desc);
		md.Run();
		md.Destroy();
	}
	
	private void RunTypeError (RotateException re)
	{
		RunGenericError (re, re.Path);
	}

	private void RunGenericError (System.Exception e, string path)
	{
		string longmsg = String.Format (Mono.Posix.Catalog.GetString ("Received error \"{0}\" while attempting to rotate {1}"),
						e.Message, System.IO.Path.GetFileName (path));

		HigMessageDialog md = new HigMessageDialog (parent_window, DialogFlags.DestroyWithParent,
							    MessageType.Warning, ButtonsType.Ok,
							    Mono.Posix.Catalog.GetString ("Error while rotating photo."),
							    longmsg);
		md.Run ();
		md.Destroy ();
	}
}
