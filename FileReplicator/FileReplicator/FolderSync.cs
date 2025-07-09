using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static FileReplicator.ServiceMessages;

namespace FileReplicator
{
    public class FolderSync
    {
        public Queue<string> GetLog() => _log;
        public List<(DirectoryInfo source, DirectoryInfo destination)> GetFolderToSync() => _foldersToSync;
        private readonly Queue<string> _log = [];
        private readonly List<(DirectoryInfo source, DirectoryInfo destination)> _foldersToSync = [];        
        public FolderSync()
        {
        }

        public void AddFolderToSync(string sourceFolder, string destinationFolder)
        {
            DirectoryInfo isourceFolder, idestinationFolder;
            isourceFolder = new DirectoryInfo(sourceFolder);
            idestinationFolder = new DirectoryInfo(destinationFolder).CreateSubdirectory(isourceFolder.Name);
            if (!isourceFolder.Exists)
                isourceFolder.Create();
            if (!idestinationFolder.Exists)
                idestinationFolder.Create();

            _foldersToSync.Add((source: isourceFolder, destination: idestinationFolder));
        }
        public void Sync()
        {
            _foldersToSync.ForEach(f => SyncFolder(f.source, f.destination));
        }
        public void SyncFolder(DirectoryInfo from, DirectoryInfo to)
        {
            var files = new Queue<(FileInfo from, FileInfo to)>();
            FileInfo[] filesToSync = from.GetFiles();
            foreach (var file in filesToSync)
            {
                if (!IsFileLocked(from.FullName))
                {
                    files.Enqueue((from: file, to: new FileInfo(to.FullName+"\\"+file.Name)));
                }
            }
            var _ = SyncFiles(files);
            
        }

        public IEnumerable<(FileInfo from, FileInfo to)> SyncFiles(Queue<(FileInfo from, FileInfo to)> files)
        {
            List<(FileInfo from, FileInfo to)> lockedFiles = [];
            while (files.Count > 0)
            {
                var file = files.Dequeue();
                if (SyncFile(file.from.FullName, file.to.FullName) == 5)
                    lockedFiles.Add(file);
            }
            return lockedFiles;
        }
        public int SyncFile(string from, string to)
        {

            SyncOperationResultsCode code = SyncOperationResultsCode.NoAction;
            if (!IsFileLocked(from))
                if (File.Exists(to))
                {
                    if (File.GetLastWriteTime(from) != File.GetLastWriteTime(to))
                    {
                        File.Copy(from, to, true);
                        code = SyncOperationResultsCode.SuccessfulOverwriting;
                    }
                    else
                        code = SyncOperationResultsCode.NoAction;
                }
                else
                {
                    File.Copy(from, to);
                    code = SyncOperationResultsCode.SuccessfulCopying;
                }
            else
                code = SyncOperationResultsCode.FileLocked;
            Log(from, to, code);
            return (int)code;
        }

        void Log(string from, string to, SyncOperationResultsCode code)
        {
            _log.Enqueue($"{LogMessages[(int)code]};{from};{to}");
        }

        public static bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                    return false;
                }
            }
            catch (IOException ex)
            {
                var errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(ex) & 0xFFFF;
                return errorCode == 32 || errorCode == 33; // 32 — ERROR_SHARING_VIOLATION, 33 — ERROR_LOCK_VIOLATION
            }
            catch (Exception)
            {
                return false;
            }
        }
    }


}
