/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ShowdownSoftware
{
    public enum DownloadState : int
    {
        Idle,
        Requesting,
        Downloading,
        Pausing,
        Paused,
        Cancelling,
        Cancelled,
        Complete,
        Failed
    }
    
    public interface IDownloadManager
    {
        string GetSavePath(string filename);
        void OnDownloadCancelled(Download download);
        void OnDownloadFailed(Download download);
        void OnDownloadComplete(Download download);
    }

    public class Download : INotifyPropertyChanged
    {
        const int MAX_CHUNKS = 8;
        const long MIN_TRANSFER_SPEED = 30 * 1024;
        const long BUFFER_SIZE = MIN_TRANSFER_SPEED;
        const int READ_TIMEOUT = 3000;
        const int REQUEST_TIMEOUT = 10000;

        FileInfo info = null;
        ObservableCollection<Chunk> chunks = new ObservableCollection<Chunk>();
        MemoryMappedFile file = null;
        CancellationTokenSource ctokenSource = null;
        Stopwatch timer = new Stopwatch();
        DownloadState state = DownloadState.Idle;
        Exception failure = null;
        int chunksCompleted = 0;
        int chunksPaused = 0;
        IDownloadManager manager = null;
        
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string name) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public FileInfo FileInfo {
            get { return info; }
        }

        public string Filename {
            get { return info.name; }
        }
        public string Url {
            get { return info.url; }
        }

        public ObservableCollection<Chunk> Chunks {
            get { return chunks; }
        }
        
        public int Progress {
            get {
                long size = Size;
                return size > 0 ? (int)(Received * 100 / size) : 0;
            }
        }

        public string Status {
            get {
                if(state == DownloadState.Downloading)
                    return $"{state} {Progress}%";
                else
                    return state.ToString();
            }
        }

        public long Size {
            get { return info.size; }
        }

        public string FormattedSize {
            get { return Util.BytesToString(Size); }
        }

        public long Received {
            get {
                long totalBytesReceived = 0;
                foreach(var c in chunks) totalBytesReceived += c.bytesReceived;
                return  totalBytesReceived;
            }
        }

        public string FormattedReceived {
            get { return Util.BytesToString(Received); }
        }
        
        public long Speed {
            get {
                long speed = 0;
                foreach(var c in chunks) speed += c.bytesPerSecond;
                return speed;
            }
        }

        public long AverageSpeed {
            get { return (long)(Received / timer.Elapsed.TotalSeconds + 0.5); }
        }

        public string FormattedSpeed {
            get {
                if(state == DownloadState.Complete)
                    return Util.BytesToString(AverageSpeed) + "/s";
                else
                    return Util.BytesToString(Speed) + "/s";
            }
        }

        public TimeSpan Elapsed {
            get { return timer.Elapsed; }
        }

        public DownloadState State {
            get { return state; }
        }
        
        public bool CanStart {
            get { return state == DownloadState.Idle || state == DownloadState.Cancelled; }
        }

        public bool CanPause {
            get { return state == DownloadState.Downloading; }
        }

        public bool IsPaused {
            get { return state == DownloadState.Paused; }
        }

        public bool CanResume {
            get { return state == DownloadState.Paused; }
        }

        public bool IsDownloading {
            get { return state == DownloadState.Downloading; }
        }

        public bool CanCancel {
            get {
                return state == DownloadState.Requesting
                    || state == DownloadState.Downloading
                    || state == DownloadState.Pausing
                    || state == DownloadState.Paused;
            }
        }

        public bool IsCancelled {
            get { return state == DownloadState.Cancelled; }
        }

        public bool IsComplete {
            get { return state == DownloadState.Complete; }
        }

        public bool HasTerminated {
            get {
                return state == DownloadState.Cancelled
                    || state == DownloadState.Failed
                    || state == DownloadState.Complete;
            }
        }
        

        public Download(IDownloadManager manager, FileInfo info) {
            this.manager = manager;
            this.info = info;
        }

        public Download(IDownloadManager manager, string url) {
            this.manager = manager;
            this.info = new FileInfo() { url = url };
        }

        public void Start() {
            if(CanStart)
                StartAsync();
        }

        public void Cancel()
        {
            if(CanCancel)
            {
                state = DownloadState.Cancelling;
                ctokenSource.Cancel();

                foreach(var chunk in chunks)
                {
                    if(chunk.state == ChunkState.Paused)
                    {
                        chunk.SetCancelled();
                        ++chunksCompleted;
                    }
                }

                if(chunksCompleted == MAX_CHUNKS)
                    CompleteDownload();
            }
        }

        public void Pause()
        {
            if(CanPause)
                state = DownloadState.Pausing;
        }

        public void Resume()
        {
            if(CanResume)
            {
                timer.Start();
                chunksPaused = 0;
                state = DownloadState.Downloading;

                foreach(var chunk in chunks)
                {
                    if(chunk.state == ChunkState.Paused)
                        DownloadChunkAsync(chunk, ctokenSource.Token);
                }
            }
        }

        private async void StartAsync()
        {
            state = DownloadState.Requesting;
            
            ctokenSource = new CancellationTokenSource();
            CancellationToken ctoken = ctokenSource.Token;

            if(info.size == 0)
            {
                try {
                    info = await GetFileInfoAsync(info.url, ctoken);
                }
                catch(OperationCanceledException) {
                    state = DownloadState.Cancelled;
                    manager.OnDownloadCancelled(this);
                    return;
                }
                catch(Exception ex) {
                    failure = ex;
                    state = DownloadState.Failed;
                    manager.OnDownloadFailed(this);
                    return;
                }
            }

            if(info.size == 0)
            {
                ctokenSource.Cancel();
                failure = new Exception("Failed to retrieve file info");
                state = DownloadState.Failed;
                manager.OnDownloadFailed(this);
                return;
            }
            
            if(string.IsNullOrEmpty(info.path))
            {
                string fullPath = manager.GetSavePath(info.name);
                if(!string.IsNullOrEmpty(fullPath))
                {
                    info.path = fullPath;
                    info.name = Path.GetFileName(fullPath);
                }
                else
                {
                    ctokenSource.Cancel();
                    state = DownloadState.Cancelled;
                    manager.OnDownloadCancelled(this);
                    return;
                }
            }
            
            try {
                file = MemoryMappedFile.CreateFromFile(info.path, FileMode.Create, info.name, info.size, MemoryMappedFileAccess.ReadWrite);
            }
            catch(Exception ex) {
                failure = ex;
                state = DownloadState.Failed;
                manager.OnDownloadFailed(this);
                return;
            }
            
            state = DownloadState.Downloading;
            timer.Start();

            long chunkSize = info.size / MAX_CHUNKS;
            long lastChunkSize = chunkSize + (info.size - chunkSize * MAX_CHUNKS);
            long rangeStart = 0;

            for(int i = 0; i < MAX_CHUNKS; ++i)
            {
                long offset = rangeStart;
                long size = i < MAX_CHUNKS - 1 ? chunkSize : lastChunkSize;
                var stream = file.CreateViewStream(offset, size, MemoryMappedFileAccess.Write);
                rangeStart += size;

                Chunk chunk = new Chunk(this)
                {
                    id = i,
                    offset = offset,
                    size = size,
                    rangeStart = offset,
                    rangeEnd = offset + size - 1,
                    stream = stream,
                    bytesReceived = 0,
                    bytesPerSecond = 0,
                    state = ChunkState.Idle
                };

                chunks.Add(chunk);
                
                DownloadChunkAsync(chunk, ctokenSource.Token);
            }
        }

        async void DownloadChunkAsync(Chunk chunk, CancellationToken ctoken)
        {
            try
            {
                chunk.state = ChunkState.Request;
                chunk.request = (HttpWebRequest)WebRequest.Create(info.url);
                chunk.request.AddRange(chunk.rangeStart, chunk.rangeEnd);
                chunk.request.ReadWriteTimeout = READ_TIMEOUT;
                chunk.request.Timeout = REQUEST_TIMEOUT;
                chunk.request.Proxy = null;
                chunk.request.Accept = "*/*";
                chunk.request.UserAgent = App.UserAgent;

                var response = (HttpWebResponse)await chunk.request.GetResponseAsync(ctoken);
                
                chunk.state = ChunkState.Download;
                
                using(var stream = response.GetResponseStream())
                {
                    TimedGate gate = new TimedGate(READ_TIMEOUT);
                    long lastReceived = chunk.bytesReceived;
                    byte[] buffer = new byte[BUFFER_SIZE];

                    while(chunk.bytesReceived < chunk.size)
                    {
                        if(state == DownloadState.Pausing)
                        {
                            chunk.state = ChunkState.Paused;

                            if(++chunksPaused + chunksCompleted == chunks.Count)
                            {
                                timer.Stop();
                                state = DownloadState.Paused;
                            }

                            return;
                        }

                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ctoken);
                        if(bytesRead > 0)
                        {
                            await chunk.stream.WriteAsync(buffer, 0, bytesRead, ctoken);
                            chunk.bytesReceived += bytesRead;
                            chunk.rangeStart += bytesRead;
                        }
                        
                        if(gate.TryEnter())
                        {
                            long received = chunk.bytesReceived - lastReceived;
                            lastReceived = chunk.bytesReceived;
                            chunk.bytesPerSecond = received / (READ_TIMEOUT / 1000);

                            if(chunk.bytesPerSecond < MIN_TRANSFER_SPEED)
                                throw new ChunkStalledException();
                        }
                    }
                    
                    chunk.SetComplete();

                    if(++chunksCompleted == MAX_CHUNKS)
                        CompleteDownload();
                }
            }
            catch(OperationCanceledException)
            {
                chunk.SetCancelled();

                if(++chunksCompleted == MAX_CHUNKS)
                    CompleteDownload();
            }
            catch(Exception)
            {
                chunk.SetStalled();
                DownloadChunkAsync(chunk, ctoken);
            }
        }
        
        void CompleteDownload()
        {
            timer.Stop();

            if(file != null) {
                file.Dispose();
                file = null;
            }

            if(failure != null || ctokenSource.IsCancellationRequested)
            {
                if(File.Exists(info.path))
                    File.Delete(info.path);

                info.path = string.Empty;
                chunks.Clear();
                timer.Reset();
                chunksCompleted = 0;
                chunksPaused = 0;

                if(failure != null)
                {
                    state = DownloadState.Failed;
                    manager.OnDownloadFailed(this);
                }
                else
                {
                    state = DownloadState.Cancelled;
                    manager.OnDownloadCancelled(this);
                }
            }
            else
            {
                state = DownloadState.Complete;
                manager.OnDownloadComplete(this);
            }
            
            ctokenSource.Dispose();
            ctokenSource = null;
        }

        async Task<FileInfo> GetFileInfoAsync(string url, CancellationToken ctoken)
        {
            FileInfo ret = new FileInfo();
            ret.url = url;
            
            var req = (HttpWebRequest)HttpWebRequest.Create(url);
            req.Proxy = null;
            req.Accept = "*/*";
            req.UserAgent = App.UserAgent;
            
            var response = (HttpWebResponse)await req.GetResponseAsync(ctoken);
            
            if(response.StatusCode == HttpStatusCode.OK)
            {
                ret.size = response.ContentLength;
                ret.type = response.ContentType;
                
                if(ret.size == 0)
                    throw new Exception("Failed to retrieve file size, or file is empty.");

                // try to get filename from content disposition
                var cd = response.Headers.GetContentDisposition();
                if(cd != null && !string.IsNullOrEmpty(cd.FileName))
                    ret.name = cd.FileName;
                
                // try to get filename from URL
                if(string.IsNullOrEmpty(ret.name))
                {
                    try {
                        Uri uri = new Uri(url);
                        string fn = Path.GetFileName(uri.AbsolutePath);
                        if(!string.IsNullOrEmpty(fn))
                            ret.name = fn;
                    }
                    catch(Exception){}
                }

                // filename unknown
                if(string.IsNullOrEmpty(ret.name))
                {
                    // try to guess extension from mime type
                    string ext = MimeTypeUtil.GetExtension(ret.type);
                    ret.name = "unknown" + ext ?? "";
                }
            }
            else
            {
                throw new Exception($"http request failed({response.StatusCode}).");
            }

            return ret;
        }
    }
}
