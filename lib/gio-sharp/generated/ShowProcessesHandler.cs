// This file was generated by the Gtk# code generator.
// Any changes made will be lost if regenerated.

namespace GLib {

	using System;

	public delegate void ShowProcessesHandler(object o, ShowProcessesArgs args);

	public class ShowProcessesArgs : GLib.SignalArgs {
		public string Message{
			get {
				return (string) Args[0];
			}
		}

		public IntPtr Processes{
			get {
				return (IntPtr) Args[1];
			}
		}

		public string[] Choices{
			get {
				return (string[]) Args[2];
			}
		}

	}
}
