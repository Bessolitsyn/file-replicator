using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        

    }
}
