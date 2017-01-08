/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

public static class WebExtensions
{
	public static async Task<WebResponse> GetResponseAsync(this WebRequest request, CancellationToken cancellationToken)
	{
		using(cancellationToken.Register(() => request.Abort(), false))
		{
			try {
				return await request.GetResponseAsync();
			}
			catch(WebException) {
				cancellationToken.ThrowIfCancellationRequested();
				throw;
			}
		}
	}

	public static ContentDisposition GetContentDisposition(this WebHeaderCollection headers)
	{
		string cd = headers.Get("Content-Disposition");
		return cd != null ? new ContentDisposition(cd) : null;
	}
}
