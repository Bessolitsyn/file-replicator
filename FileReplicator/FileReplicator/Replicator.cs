using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("FileReplicatorTests")]

namespace FileReplicator
{
    public class Replicator(Settings settings)
    {
        private Settings _settings = settings;
        private int _status = 0;

        /// <summary>
        /// 0 - file replication is stopped, 1 - started
        /// </summary>
        public int Status { get=>_status; }
        void Start()
        {
            _status = 1;

        }
        void Stop()
        { 
            _status = 0;
            
        }
        public void FindFilesToReplicate()
        { }
        public void CompareFilesByUpdatedTime()

        { }

    }
}
