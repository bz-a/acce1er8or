/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

function updateEnabledCheck() {
	var background = chrome.extension.getBackgroundPage();
	var isEnabled = background.isEnabled;
	
	var isEnabledImg = document.getElementById('isEnabledImg');
	var enabledSpan = document.getElementById('isEnabled');
	
	isEnabledImg.src = isEnabled ? "checkmark16.png" : "xmark16.png";
	enabledSpan.innerHTML = isEnabled ? "Enabled" : "Disabled";
}

document.getElementById("enabledButton").onclick = function(){
	var background = chrome.extension.getBackgroundPage();
	var isEnabled = background.isEnabled;
	isEnabled = !isEnabled;
	background.isEnabled = isEnabled;
	
	updateEnabledCheck();
	
	chrome.storage.sync.set(
		{ "isEnabled": isEnabled },
		function(){}
	);
};

document.getElementById("optionsButton").onclick = function(){
	chrome.runtime.openOptionsPage();
};

document.addEventListener('DOMContentLoaded', updateEnabledCheck);
