//
// DelayedOperation.cs
//
// Author:
//   Ruben Vermeersch <ruben@savanne.be>
//
// Copyright (C) 2010 Novell, Inc.
// Copyright (C) 2010 Ruben Vermeersch
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

namespace FSpot.Utils
{
    public class DelayedOperation
    {
        object syncHandle = new object ();

        public DelayedOperation (uint interval, GLib.IdleHandler op)
        {
            this.op = op;
            this.interval = interval;
        }

        public DelayedOperation (GLib.IdleHandler op)
        {
            this.op = op;
        }

        uint source;
        uint interval;

        GLib.IdleHandler op;

        bool HandleOperation ()
        {
            lock (syncHandle) {
                bool runagain = op ();
                if (!runagain)
                    source = 0;
                
                return runagain;
            }
        }

        public void Start ()
        {
            lock (syncHandle) {
                if (IsPending)
                    return;
                
                if (interval != 0)
                    source = GLib.Timeout.Add (interval, new GLib.TimeoutHandler (HandleOperation));
                else
                    source = GLib.Idle.Add (new GLib.IdleHandler (HandleOperation));
            }
        }

        public bool IsPending {
            get { return source != 0; }
        }

        public void Connect (Gtk.Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException ("obj");
            obj.Destroyed += HandleDestroy;
        }

        void HandleDestroy (object sender, EventArgs args)
        {
            Stop ();
        }

        public void Stop ()
        {
            lock (syncHandle) {
                if (IsPending) {
                    GLib.Source.Remove (source);
                    source = 0;
                }
            }
        }

        public void Restart ()
        {
            Stop ();
            Start ();
        }
    }
}
