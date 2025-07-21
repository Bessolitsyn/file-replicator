using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("FileReplicatorTests")]

namespace FileReplicator
{
    public class Replicator(Settings settings) :IDisposable
    {
        private Settings _settings = settings;
        private int _status = 0;
        private List<FolderSync> _syncExecuters = [];
        private bool _isReady = false;

        /// <summary>
        /// 0 - file replication is stopped, 1 - started
        /// </summary>
        public int Status { get=>_status; }
        internal void Start()
        {
            _status = 1;
            _syncExecuters.ForEach(x => {x.Start(); });

        }
        internal async Task StopAsync()
        {
            _syncExecuters.ForEach(x => { _ = x.StopAsync(); });
            foreach (var sync in _syncExecuters)
            { 
                await sync.StopAsync();
            }
            _status = 0;
            
        }
        
        public void FindFilesToReplicate()
        { }
        public void CompareFilesByUpdatedTime()
        { }
        /// <summary>
        /// Принудительная репликация файлов.
        /// </summary>
        internal void Execute()
        {

            if (!_isReady)
                Init();
            _syncExecuters.ForEach(e => e.Sync());
        }

        private void Init()
        {
            foreach (var item in _settings.SourceFolders)
            {
                var fc = new FolderSync();
                fc.AddFolderToSync(item.OriginalPath, item.DestinationPath);
                _syncExecuters.Add(fc);
            }
            _isReady = true;
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
