/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

var filterMode = 1;
var filters = ["*"];

chrome.storage.sync.get(
	{
		"filterMode": 1,
		"filters": ['*']
	},
	function(items)
	{
		filterMode = items["filterMode"];
		filters = items["filters"];
	}
);

chrome.downloads.onDeterminingFilename.addListener(
	function(item)
	{
		if(item.fileSize < 1000000)
			return;
		
		var url = item.finalUrl.split(/[?#]/)[0];
		var extParts = url.split('.');
		var ext = extParts[extParts.length - 1];
		var hasFilter = filters.indexOf("*") != -1 || filters.indexOf(ext) != -1;
		
		if((filterMode == 1 && hasFilter) || (filterMode == 0 && !hasFilter))
		{
			var msg = {
				url: item.finalUrl,
				name: item.filename,
				type: item.mime,
				size: item.fileSize
			};
			
			try { chrome.downloads.cancel(item.id); }
			catch(err){}
			
			chrome.runtime.sendNativeMessage(
				'com.showdownsoftware.acce1er8or',
				msg,
				function(response){
					if(!response && chrome.runtime.lastError) {
						alert("Error: Failed to communicate with desktop client.\n\nPlease make sure that the Acce1er8or desktop client is available, and has been run at least once. The download URL for the desktop client can be found on the Options page.");
					}
				}
			);
		}
	}
);

/*
chrome.runtime.onInstalled.addListener(function(obj) {
	if(obj.reason === 'install') {
		chrome.runtime.openOptionsPage();
	}
});
*/
