
namespace FSpot {
	public delegate void AreaUpdatedHandler (object sender, Gdk.Rectangle area);
	public delegate void AreaPreparedHandler (object sender, System.EventArgs args);
	
	public class AsyncPixbufLoader : System.IDisposable {
		System.IO.Stream stream;
		Gdk.PixbufLoader loader;		
		string path;
		bool area_prepared = false;
		bool done_reading = false;
		System.Exception error;
		Gdk.Pixbuf pixbuf;
		PixbufOrientation orientation;

		//byte [] buffer = new byte [8192];
		byte [] buffer = new byte [32768];

		public event AreaUpdatedHandler AreaUpdated;
		public event AreaPreparedHandler AreaPrepared;
		public event System.EventHandler Done;

		Delay delay;

		public AsyncPixbufLoader ()
		{
			delay = new Delay (new GLib.IdleHandler (AsyncRead));
		}
		
		public bool Loading
		{
			get {
				return done_reading;
			}
		}
		
		public void Load (string filename)
		{
			delay.Stop ();
			path = filename;

			if (!done_reading)
				Close ();

			done_reading = false;
			area_prepared = false;

			orientation = PixbufUtils.GetOrientation (filename);

			stream = new System.IO.FileStream (filename, System.IO.FileMode.Open, System.IO.FileAccess.Read);
			
			loader = new Gdk.PixbufLoader ();
			loader.AreaPrepared += HandleAreaPrepared;
			loader.AreaUpdated += HandleAreaUpdated;
			loader.Closed += HandleClosed;
			
			delay.Start ();
		}			


		public Gdk.PixbufLoader Loader {
			get {
				return loader;
			}
		}

	        public void LoadToAreaPrepared ()
		{
			delay.Stop  ();
			while (!area_prepared && AsyncRead ())
				; //step
		}

		public void LoadToDone ()
		{
			delay.Stop ();
			while (!done_reading && AsyncRead ())
				; //step
		}

		private void Close () 
		{
			try {
				if (loader != null) {
					loader.Close ();
					loader.Dispose ();
				}
			} catch (System.Exception e) {
				if (pixbuf != null)
					pixbuf.Dispose ();

				pixbuf = null;
			} finally {
				if (stream != null)
					stream.Close ();
			}
		}

		private bool AsyncRead () 
		{
			System.DateTime start_time = System.DateTime.Now;
			System.TimeSpan span = start_time - start_time;

			do {
				span = System.DateTime.Now - start_time;

				int len = stream.Read (buffer, 0, buffer.Length);
				loader.Write (buffer, (uint)len);
				
				if (len <= 0) {
					done_reading = true;
					Close ();
					return false;
				}
			} while (!done_reading && span.TotalMilliseconds <= 300);
			return true;
		}
		
		private void HandleAreaPrepared (object sender, System.EventArgs args)
		{
			Gdk.Pixbuf old = pixbuf;
			pixbuf = PixbufUtils.TransformOrientation (loader.Pixbuf, orientation);

			area_prepared = true;			
			if (AreaUpdated != null)
				AreaPrepared (this, System.EventArgs.Empty);

			if (old != null) {
				old.Dispose ();
			}
		}

		public Gdk.Pixbuf Pixbuf {
			get {
				if (!area_prepared)
					throw new System.Exception ("Load in progress but Pixbuf not ready");

				return pixbuf;
			}
		}
	       
		private void HandleAreaUpdated (object sender, Gdk.AreaUpdatedArgs args)
		{
			Gdk.Rectangle area = new Gdk.Rectangle (args.X, args.Y, args.Width, args.Height);

			if (pixbuf != null && loader.Pixbuf != pixbuf)
				area = PixbufUtils.TransformAndCopy (loader.Pixbuf, pixbuf, orientation, area);

			if (AreaUpdated != null)
				AreaUpdated (this, area);
		}

		private void HandleClosed (object sender, System.EventArgs args) 
		{
			if (done_reading && pixbuf != null) {
				PhotoLoader.ValidateThumbnail (path, pixbuf);
			}

			if (Done != null)
				Done (this, System.EventArgs.Empty);
		}

		public void Dispose ()
		{
			loader.AreaPrepared -= HandleAreaPrepared;
			loader.AreaUpdated -= HandleAreaUpdated;
			loader.Closed -= HandleClosed;
			Close ();
			pixbuf.Dispose ();
		}
	}
}

	       
