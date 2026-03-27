using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("FileReplicatorTests")]

namespace FileReplicator
{
    public class Replicator(Settings settings, ILogger logger ) :IDisposable
    {
        private readonly Settings _settings = settings;
        private readonly ILogger _logger = logger;
        private readonly List<FolderSync> _syncExecuters = [];
        private int _status = 0;
        private bool _isReadyToObserve = false;
        private bool _isReadyToStart = false;

        /// <summary>
        /// 0 - file replication is stopped, 1 - started
        /// </summary>
        public int Status { get=>_status; }
        public void Start()
        {
            if (!_isReadyToObserve) InitToObserve();
            _syncExecuters.ForEach(x => { x.StartObserving(); });
            _status = 1;
        }
        public async Task StopAsync()
        {            
            foreach (var sync in _syncExecuters)
            { 
                await sync.StopObservingAsync();
            }
            _status = 0;
            
        }
        
        /// <summary>
        /// Принудительная репликация файлов.
        /// </summary>
        public async Task ExecuteAsync()
        {
            
            if (!_isReadyToStart) InitToStart();
            _logger.Log(LogLevel.Information, "==| Reptor is running");
            foreach (var item in _syncExecuters)
            {
                var tstart = DateTime.Now;
                _logger.Log(LogLevel.Information, $"==| Folders item<{item.ToString()}> is processing");
                await item.ReplicateAsync();
                var dur = DateTime.Now - tstart;
                _logger.Log(LogLevel.Information, $"==| Folders item has finished with duration: {dur.Minutes}min{dur.Seconds}sec");

            }
            _logger.Log(LogLevel.Information, "==| Reptor has finished");
        }

        private void InitToStart()
        {
            foreach (var item in _settings.SourceFolders)
            {
                var fc = new FolderSync(_logger);
                fc.SerFolderToSync(new DirectoryInfo(item.OriginalPath), new DirectoryInfo(item.DestinationPath));                
                //fc.SerFolderToSync(new DirectoryInfo("c:\\temp\\ReptorTests\\Version7"), new DirectoryInfo(item.DestinationPath));
                _syncExecuters.Add(fc);
            }
            _isReadyToStart= true;
        }
        private void InitToObserve()
        {
            foreach (var item in _settings.SourceFolders)
            {
                var fc = new FolderSync(_logger);
                fc.SetFolderToObserve(new DirectoryInfo(item.OriginalPath), new DirectoryInfo(item.DestinationPath));
                _syncExecuters.Add(fc);
            }
            _isReadyToObserve = true;
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
