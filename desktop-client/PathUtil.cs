/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System.IO;
using System.Reflection;

public class PathUtil
{
	private static int maxPath = -1;

	public static int MaxPath {
		get {
			if(maxPath == -1)
			{
				BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
				FieldInfo field = typeof(Path).GetField("MaxPath", flags);
				if(field != null)
					maxPath = (int)field.GetValue(null);
				else
					 maxPath = 255;
			}

			return maxPath;
		}
	}

	public static void ShowInExplorer(string filePath)
	{
        if(File.Exists(filePath))
			System.Diagnostics.Process.Start("explorer.exe", $@"/select, ""{filePath}""");
	}

	public static void OpenFile(string filePath)
	{
		if(File.Exists(filePath))
			System.Diagnostics.Process.Start("explorer.exe", $@"""{filePath}""");
	}
}
