/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using System.IO;
using System.Net;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.VisualBasic.FileIO;

namespace ShowdownSoftware
{
	public partial class MainWindow : Window, IDownloadManager
	{
		ObservableCollection<Download> downloads = new ObservableCollection<Download>();
		DispatcherTimer updateTimer;

		int _displayedPercentage = -1;
		public int displayedPercentage
		{
			get { return _displayedPercentage; }
			set
			{
				if(_displayedPercentage != value)
				{
					_displayedPercentage = value;
					if(_displayedPercentage < 0)
					{
						this.Title = "Acce1er8or";
						this.TaskbarItemInfo.ProgressValue = 0;
					}
					else
					{
						this.Title = string.Format("{0}% - Acce1er8or", value);
						this.TaskbarItemInfo.ProgressValue = value / 100.0;
					}
				}
			}
		}

		public bool IsClosePending {
			get; set;
		}

		public MainWindow()
		{
			InitializeComponent();

			ServicePointManager.DefaultConnectionLimit = 128;

			this.TaskbarItemInfo = new TaskbarItemInfo() {
				ProgressState = TaskbarItemProgressState.Normal
			};

			downloadList.ItemsSource = downloads;

			ChromeHost.StartHost(this.OnChromeMessage, this.OnChromeError);
			this.Closing += (s, e) => { ChromeHost.StopHost(); };

			updateTimer = new DispatcherTimer();
			updateTimer.Interval = TimeSpan.FromMilliseconds(200);
			updateTimer.Tick += UpdateUI;
		}
		
		void DoUpdate() {
			UpdateUI(this, EventArgs.Empty);
		}

		bool ShouldUpdate
		{
			get {
				if(WindowState == WindowState.Minimized)
					return false;

				bool activeDownloads = false;

				foreach(var dl in downloads)
				{
					if(dl.State != DownloadState.Cancelled
					&& dl.State != DownloadState.Failed
					&& dl.State != DownloadState.Complete
					&& dl.State != DownloadState.Paused)
					{
						activeDownloads = true;
						break;
					}
				}

				return activeDownloads;
			}
		}
		
		private void UpdateUI(object sender, EventArgs e)
		{
			foreach(var dl in downloads)
			{
				dl.NotifyPropertyChanged(null);

				if(downloadList.SelectedItem == dl)
				{
					foreach(var chunk in dl.Chunks)
						chunk.NotifyPropertyChanged(null);
				}
			}
			
			updateTimer.IsEnabled = this.ShouldUpdate;
		}

		protected override void OnStateChanged(EventArgs e) {
			updateTimer.IsEnabled = this.ShouldUpdate;
		}
		
		public string GetSavePath(string filename)
		{
			string ext = Path.GetExtension(filename);
			string filter = !string.IsNullOrEmpty(ext) ?
				ext.Substring(1).ToUpper() + " Files|*" + ext
				: "All Files|*.*";

			Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
			dialog.Title = "Save File";
			dialog.Filter = filter;
			dialog.FileName = filename;
			dialog.InitialDirectory = KnownFolders.GetPath(KnownFolders.Downloads);

			bool? res = dialog.ShowDialog(this);
			
			if(res.Value == false || string.IsNullOrEmpty(dialog.FileName))
				return null;
			
			return dialog.FileName;
		}
		
		public void OnDownloadCancelled(Download download)
		{
			if(IsClosePending)
			{
				bool stillDownloading = false;

				foreach(var dl in downloads) {
					if(!dl.HasTerminated)
						stillDownloading = true;
				}

				if(!stillDownloading)
				{
					IsClosePending = false;
					this.Close();
				}
			}

			DoUpdate();
		}

		public void OnDownloadFailed(Download download)
		{
			DoUpdate();
		}

		public void OnDownloadComplete(Download download)
		{
			DoUpdate();
		}
		
		void OnChromeMessage(ChromeMessage msg)
		{
			string url = msg["url"] as string;
			string filename = msg["name"] as string;
			string type = msg["type"] as string;
			long size = Convert.ToInt64(msg["size"]);
			
			var info = new FileInfo() {
				url = url,
				name = filename,
				path = "",
				size = size,
				type = type
			};
			
			Activate();

			var dl = new Download(this, info);
			downloads.Add(dl);
			dl.Start();
			DoUpdate();
		}

		void OnChromeError(Exception ex)
		{
			
		}
		
		private void btnAdd_Click(object sender, RoutedEventArgs e)
		{
			string url = txtURL.Text;
			txtURL.Text = "";

			if(!string.IsNullOrEmpty(url))
			{
				var dl = new Download(this, url);
				downloads.Add(dl);
				dl.Start();
				DoUpdate();
			}
		}

		private void downloadList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			chunkList.ItemsSource = item?.Chunks;
		}

		private void downloadList_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			downloadList.UnselectAll();
		}
		
		private void downloadList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			var item = downloadList.SelectedItem as Download;
			if(item != null && item.State == DownloadState.Complete)
				PathUtil.OpenFile(item.FileInfo.path);
		}

		private void downloadList_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if(e.Key == Key.Delete)
			{
				var item = downloadList.SelectedItem as Download;
				if(item != null)
				{
					if(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
					{
						if(item.CanCancel)
						{
							item.Cancel();
							downloads.Remove(item);
						}
						else if(item.HasTerminated)
						{
							//if(File.Exists(item.FileInfo.path))
							//	File.Delete(item.FileInfo.path);

							if(File.Exists(item.FileInfo.path))
								FileSystem.DeleteFile(item.FileInfo.path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
							
							downloads.Remove(item);
						}
					}
					else
					{
						if(item.CanCancel)
							item.Cancel();
						else if(item.HasTerminated)
							downloads.Remove(item);
					}

					DoUpdate();
				}
			}
			else if(e.Key == Key.Enter)
			{
				var item = downloadList.SelectedItem as Download;
				if(item != null)
				{
					var state = item.State;
					if(state == DownloadState.Cancelled)
					{
						item.Start();
						DoUpdate();
					}
				}
			}
		}
		
		private void downloadList_menuOpen(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			PathUtil.OpenFile(item?.FileInfo.path);
		}

		private void downloadList_menuOpenFolder(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			PathUtil.ShowInExplorer(item?.FileInfo.path);
		}

		private void downloadList_menuCopyURL(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			Clipboard.SetText(item.Url);
		}
		
		private void downloadList_menuPause(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			item.Pause();
			DoUpdate();
		}

		private void downloadList_menuResume(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			item.Resume();
			DoUpdate();
		}
		
		private void downloadList_menuCancel(object sender, RoutedEventArgs e)
		{
			var item = downloadList.SelectedItem as Download;
			item.Cancel();
			DoUpdate();
		}
		
		private void downloadList_menuCancelRemove(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			item.Cancel();
			downloads.Remove(item);
			DoUpdate();
		}

		private void downloadList_menuRemove(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			downloads.Remove(item);
			DoUpdate();
		}

		private void downloadList_menuRemoveDelete(object sender, RoutedEventArgs e) {
			var item = downloadList.SelectedItem as Download;
			//if(File.Exists(item.FileInfo.path))
			//	File.Delete(item.FileInfo.path);

			if(File.Exists(item.FileInfo.path))
				FileSystem.DeleteFile(item.FileInfo.path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);

			downloads.Remove(item);
			DoUpdate();
		}
		
		private void chunkList_menuReconnect(object sender, RoutedEventArgs e) {
			var chunk = chunkList.SelectedItem as Chunk;
			chunk?.request?.Abort();
		}

		private void chunkList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			var chunk = chunkList.SelectedItem as Chunk;
			chunk?.request?.Abort();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if(IsClosePending) {
				e.Cancel = true;
				return;
			}

			bool stillDownloading = false;
			
			foreach(var dl in downloads) {
				if(dl.CanCancel)
					stillDownloading = true;
			}

			if(stillDownloading)
			{
				var res = MessageBox.Show(
					"Quitting will cancel all downloads.",
					"Continue?",
					MessageBoxButton.OKCancel,
					MessageBoxImage.Question);

				if(res != MessageBoxResult.OK)
				{
					e.Cancel = true;
					return;
				}
				
				stillDownloading = false;

				foreach(var dl in downloads)
				{
					dl.Cancel();

					if(!dl.HasTerminated)
						stillDownloading = true;
				}
				
				if(stillDownloading)
				{
					e.Cancel = true;
					IsClosePending = true;
					DoUpdate();
				}
			}
		}
	}
}
