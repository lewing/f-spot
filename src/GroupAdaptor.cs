

namespace FSpot {
	public interface ILimitable {
		void SetLimits (int min, int max);
	}
	
	public abstract class GroupAdaptor {
		public abstract int Value (int item) ;
		public abstract int Count ();
		public abstract string TickLabel (int item);
		public abstract string GlassLabel (int item);

		public abstract void Reload ();

		public abstract void SetGlass (int item);

		public delegate void GlassSetHandler (GroupAdaptor adaptor, int index);
		public virtual event GlassSetHandler GlassSet;

		public delegate void ChangedHandler (GroupAdaptor adaptor);
		public virtual event ChangedHandler Changed;
	}
}
