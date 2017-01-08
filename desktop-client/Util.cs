/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

namespace ShowdownSoftware
{
	public static class Util
	{
		public static string BytesToString(long bytes)
		{
			if(bytes < 1000)
				return string.Format("{0} B", bytes);
			else if(bytes < 1000000)
				return string.Format("{0:0.0} KB", bytes / 1000.0);
			else if(bytes < 1000000000)
				return string.Format("{0:0.0} MB", bytes / 1000000.0);
			else
				return string.Format("{0:0.0} GB", bytes / 1000000000.0);
		}
	}
}
