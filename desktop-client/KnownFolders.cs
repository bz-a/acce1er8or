/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.Runtime.InteropServices;

namespace ShowdownSoftware
{
	class KnownFolders
	{
		public static readonly Guid Contacts      = new Guid("56784854-C6CB-462B-8169-88E350ACB882");
        public static readonly Guid Desktop       = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
        public static readonly Guid Documents     = new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
        public static readonly Guid Downloads     = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        public static readonly Guid Favorites     = new Guid("1777F761-68AD-4D8A-87BD-30B759FA33DD");
        public static readonly Guid Links         = new Guid("BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968");
        public static readonly Guid Music         = new Guid("4BD8D571-6D19-48D3-BE97-422220080E43");
        public static readonly Guid Pictures      = new Guid("33E28130-4E1E-4676-835A-98395C3BC3BB");
        public static readonly Guid SavedGames    = new Guid("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4");
        public static readonly Guid SavedSearches = new Guid("7D1D3A04-DEBB-4115-95CF-2F29DA2920DA");
        public static readonly Guid Videos        = new Guid("18989B1D-99B5-455B-841C-AB7C74E4DDFC");

		public static string GetPath(Guid folderGuid)
		{
			string ret = null;
			SHGetKnownFolderPath(folderGuid, 0, IntPtr.Zero, out ret);
			return ret;
		}

		[DllImport("shell32.dll", CharSet=CharSet.Unicode)]
		static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out string pszPath);
	}
}
