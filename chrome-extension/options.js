/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

function removeFilter(table, row)
{
	document.getElementById('filter-table').deleteRow(row.rowIndex);
	
	if(table.rows.length == 0)
		insertFilter(table, null, 0);
	
	document.getElementById('apply').disabled = false;
}

function insertFilter(table, filt, i)
{
	var row = table.insertRow(i);
	
	var cell = row.insertCell(0);
	cell.innerHTML = filt ? filt : "&nbsp;";
	cell.className = "filter-item";
	cell.style = "width:100%;";
		
	if(filt)
	{
		var cell2 = row.insertCell(1);
		cell2.style = "border:1px solid black;";
		var btn = document.createElement("button");
		var lab = document.createTextNode("Remove");
		btn.appendChild(lab);
		btn.style = "margin:0px; padding:0px;";
		btn.addEventListener('click', function(){ removeFilter(table, row); });
		btn.title = "remove this extension from the filter";
		cell2.appendChild(btn);
	}
	else
	{
		cell.style = "width:100%; height:24px;";
	}
}

function updateBackgroundSettings(filterMode, filters, isEnabled) {
	var background = chrome.extension.getBackgroundPage();
	background.filterMode = filterMode;
	background.filters = filters;
	background.isEnabled = isEnabled;
}

function restoreOptions()
{
	chrome.storage.sync.get(
		{
			"filterMode": 1,
			"filters": ['*'],
			"isEnabled": true
		},
		function(items)
		{
			document.getElementById('isEnabledCheck').checked = items["isEnabled"];
			document.getElementById('filterMode').value = items["filterMode"];
			
			var filterTable = document.getElementById('filter-table');
			
			var filters = items["filters"];
			var len = filters.length;
			
			for(var i = 0; i < len; i++)
				insertFilter(filterTable, filters[i], i);
			
			if(len == 0)
				insertFilter(table, null, 0);
		}
	);
}

function saveOptions()
{
	var filterMode = document.getElementById('filterMode').value;
	var filters = [];
	var isEnabled = document.getElementById('isEnabledCheck').checked;
	
	var filterTable = document.getElementById('filter-table');
	var len = filterTable.rows.length;
	
	for(var i = 0; i < len; i++)
	{
		var cell = filterTable.rows[i].cells[0];
		var filt = cell.innerHTML;
		
		if(filters.indexOf(filt) == -1)
			filters.push(filt);
	}
	
	chrome.storage.sync.set(
		{
			"filterMode": filterMode,
			"filters": filters,
			"isEnabled": isEnabled
		},
		function()
		{
			document.getElementById('apply').disabled = true;
			updateBackgroundSettings(filterMode, filters, isEnabled);
		}
	);
}

function onOptionChanged() {
	document.getElementById('apply').disabled = false;
}

function addFilter()
{
	var filt = document.getElementById('extension').value;
	if(!filt) return;
	
	var filterTable = document.getElementById('filter-table');
	var len = filterTable.rows.length;
	
	if(len == 1 && filterTable.rows[0].cells[0].innerHTML == "&nbsp;")
	{
		filterTable.deleteRow(0);
		len = 0;
	}
	
	insertFilter(filterTable, filt, len);
	
	document.getElementById('extension').value = "";
	document.getElementById('apply').disabled = false;
}

document.addEventListener('DOMContentLoaded', restoreOptions);
document.getElementById('apply').addEventListener('click', saveOptions);
document.getElementById('filterMode').addEventListener('change', onOptionChanged);
document.getElementById('isEnabledCheck').addEventListener('change', onOptionChanged);
document.getElementById('add-filter').addEventListener('click', addFilter);