/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Net;
using System.ComponentModel;

namespace ShowdownSoftware
{
	public class ChunkStalledException : Exception { }

	public enum ChunkState
	{
		Idle,
		Request,
		Download,
		Stalled,
		Paused,
		Cancel,
		Failure,
		Complete,
	}

	public class Chunk : INotifyPropertyChanged
	{
		public Download download;
		public int id;
		public long offset;
		public long size;
		public long rangeStart;
		public long rangeEnd;
		public Stream stream;
		public long bytesReceived;
		public long bytesPerSecond;
		public ChunkState state;
		public HttpWebRequest request;

		public event PropertyChangedEventHandler PropertyChanged;
		public void NotifyPropertyChanged(string name) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		public Chunk(Download download) {
			this.download = download;
		}

		public int Progress {
			get { return size > 0 ? (int)(bytesReceived * 100 / size) : 0; }
		}

		public string FormattedProgress {
			get {
				switch(state)
				{
				default:
				case ChunkState.Idle:
					return "0 B - 0 B/s";
				case ChunkState.Request:
					return "Waiting for Response";
				case ChunkState.Download:
					return string.Format("{0:n0} B - {1}/s", bytesReceived, Util.BytesToString(bytesPerSecond));
				case ChunkState.Stalled:
					return "Transfer Stalled";
				case ChunkState.Paused:
					return "Paused";
				case ChunkState.Cancel:
					return "Cancelled";
				case ChunkState.Failure:
					return "Failed";
				case ChunkState.Complete:
					return "Complete";
				}
			}
		}

		public void SetCancelled()
		{
			if(stream != null)
			{
				stream.Close();
				stream = null;
			}

			request = null;
			bytesPerSecond = 0;
			state = ChunkState.Cancel;
		}

		public void SetComplete()
		{
			if(stream != null)
			{
				stream.Close();
				stream = null;
			}

			request = null;
			bytesPerSecond = 0;
			state = ChunkState.Complete;
		}

		public void SetStalled()
		{
			request = null;
			bytesPerSecond = 0;
			state = ChunkState.Stalled;
		}
	}
}
