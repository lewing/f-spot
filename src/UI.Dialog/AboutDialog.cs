/*
 * FSpot.UI.Dialog.AboutDialog.cs
 *
 * Author(s):
 *	Stephane Delcroix <stephane@delcroix.org>
 *
 * This is free software. See COPYING for details.
 */

using System;
using Mono.Unix;

namespace FSpot.UI.Dialog
{
	public class AboutDialog : Gtk.AboutDialog
	{
		private static AboutDialog about = null;
		
		private AboutDialog () {
			Artists = new string [] {
				"Jakub Steiner",	
			};
			Authors = new string [] {
				"Primary Development",
				"\tLawrence Ewing",
				"\tStephane Delcroix",
				"",
				"Active Contributors to this release",
				"\tLorenzo Milesi",
				"",
				"Contributors",
				"\tAaron Bockover",
				"\tAlessandro Gervaso",
				"\tAlex Graveley",
				"\tAlvaro del Castillo",
				"\tBen Monnahan",
				"\tBengt Thuree",
				"\tChad Files",
				"\tEttore Perazzoli",
				"\tEwen Cheslack-Postava",
				"\tJoe Shaw",
				"\tJoerg Buesse",
				"\tJon Trowbridge",
				"\tJoshua Tauberer",
				"\tGabriel Burt",
				"\tGrahm Orr",
				"\tLaurence Hygate",
				"\tLee Willis",
				"\tMartin Willemoes Hansen",
				"\tMatt Jones",
				"\tMiguel de Icaza",
				"\tNat Friedman",
				"\tPatanjali Somayaji",
				"\tPeter Johanson",
				"\tRuben Vermeersch",
				"\tTambet Ingo",
				"\tThomas Van Machelen",
				"\tTodd Berman",
				"\tVincent Moreau",
				"\tVladimir Vukicevic",
				"\tXavier Bouchoux",
				"",
				"In memory Of",
				"\tEttore Perazzoli",
			};
			Comments = "Photo management for GNOME";
			Copyright = Catalog.GetString ("Copyright \x00a9 2003-2008 Novell Inc.");
			Documenters = new string[] {
				"Aaron Bockover",
				"Alexandre Prokoudine",	
				"Bengt Thuree",
				"Gabriel Burt",
				"Miguel de Icaza",
				"Stephane Delcroix",
			};
			License = "GPL v2";
			Logo = PixbufUtils.LoadFromAssembly ("f-spot-logo.svg");
	#if !GTK_2_11
			Name = "F-Spot";
	#endif
			TranslatorCredits = Catalog.GetString ("translator-credits");
                	if (System.String.Compare (TranslatorCredits, "translator-credits") == 0)
                		TranslatorCredits = null;
			Version = Defines.VERSION;
			Website = "http://f-spot.org";
			WebsiteLabel = Catalog.GetString ("F-Spot Website");
			WrapLicense = true;
		}

		public static AboutDialog ShowUp ()
		{
			if (about == null)
				about = new AboutDialog ();
			about.Destroyed += delegate (object o, EventArgs e) {about = null;};
			about.Response += delegate (object o, Gtk.ResponseArgs e) {if (about != null) about.Destroy ();};
			about.Show ();
			return about;
		}
	}
}