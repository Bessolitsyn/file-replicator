using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileReplicator
{
    public static class FileInfoExtensions
    {
        public static FileInfo CopyToDir(this FileInfo fileInfo, DirectoryInfo dir)
        {
            return fileInfo.CopyTo(Path.Combine(dir.FullName, fileInfo.Name));
        }
    }
}
