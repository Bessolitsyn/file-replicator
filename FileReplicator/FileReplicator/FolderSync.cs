using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static FileReplicator.ServiceMessages;

[assembly: InternalsVisibleTo("FolderSyncTests")]

namespace FileReplicator
{
    public class FolderSync
    {
        public Queue<string> GetLog() => _log;
        public List<(DirectoryInfo source, DirectoryInfo destination)> GetFolderToSync() => _foldersToSync;
        private readonly Queue<string> _log = [];
        private readonly List<(DirectoryInfo source, DirectoryInfo destination)> _foldersToSync = [];
        private List<FileSystemWatcher> _observers = [];
        private readonly CancellationTokenSource _cancellationTokenSource = new();    
        private Task _observersTask;

        public FolderSync()
        { }

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
            _observers.Add(new FileSystemWatcher(isourceFolder.FullName));
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
                files.Enqueue((from: file, to: new FileInfo(to.FullName + "\\" + file.Name)));
            }
            var _ = SyncFiles(files);

        }

        public IEnumerable<(FileInfo from, FileInfo to)> SyncFiles(Queue<(FileInfo from, FileInfo to)> files)
        {
            List<(FileInfo from, FileInfo to)> lockedFiles = [];
            while (files.Count > 0)
            {
                var file = files.Dequeue();
                if (SyncFile(file.from, ref file.to) == 5)
                    lockedFiles.Add(file);
            }
            return lockedFiles;
        }
        public int SyncFile(FileInfo from, ref FileInfo to)
        {
            from.Refresh();
            to.Refresh();
            SyncOperationResultsCode code = SyncOperationResultsCode.NoAction;
            if (!IsFileLocked(from))
                if (to.Exists && !IsFileLocked(to))
                {
                    if (!AreFilesEquivalent(from, to))
                    {
                        from.CopyTo(to.FullName, true);
                        code = SyncOperationResultsCode.SuccessfulOverwriting;
                    }
                    else
                        code = SyncOperationResultsCode.NoAction;
                }
                else
                {
                    if (to.Directory == null || !to.Directory.Exists)
                        throw new Exception(ExceptionMessages[(int)ExceptionMessageCode.NoDestinationFolder]);
                    else
                    {
                        from.CopyTo(to.FullName);
                        code = SyncOperationResultsCode.SuccessfulCopying;
                    }
                }
            else
                code = SyncOperationResultsCode.FileLocked;
            to.Refresh();
            Log(from.FullName, to.FullName, code);
            return (int)code;
        }

        void Log(string from, string to, SyncOperationResultsCode code)
        {
            _log.Enqueue($"{DateTime.Now}{LogMessages[(int)code]};{from};{to}");
        }

        public static bool IsFileLocked(FileInfo file)
        {

            try
            {
                using (FileStream stream = File.Open(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
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
        public static bool AreFilesEquivalent(FileInfo from, FileInfo to)
        {
            return from.LastWriteTime == to.LastWriteTime;
        }



        /// <summary>
        /// Запуск мониторинга папок
        /// </summary>
        public void Start()
        {
            _observers.ForEach(o =>
            {
                o.NotifyFilter = NotifyFilters.LastWrite
                                  | NotifyFilters.FileName
                                  | NotifyFilters.DirectoryName;

                //o.Changed += OnFileChanged;
                //o.Created += OnFileChanged;
                //o.Deleted += OnFileChanged;
                //o.Renamed += OnFileRenamed;
                o.EnableRaisingEvents = true;
            });

            // Запускаем асинхронный мониторинг с возможностью отмены
            _observersTask = Task.Run(() => MonitorFolderAsync(_cancellationTokenSource.Token));
        }
        /// <summary>
        /// Остановка мониторинга папок
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            _observers.ForEach(o=>o.EnableRaisingEvents = false);
            _cancellationTokenSource.Cancel(); // Отправляем сигнал отмены

            try
            {
                await _observersTask; // Ждём завершения задачи
            }
            catch (OperationCanceledException)
            {
                //Мониторинг остановлен
                
            }
            finally
            {
                _observers.ForEach(o => o.Dispose());
                _cancellationTokenSource.Dispose();
            }
        }
        private async Task MonitorFolderAsync(CancellationToken cancellationToken)
        {

            while (!cancellationToken.IsCancellationRequested)
            {
                // Здесь можно добавить периодическую проверку, если нужно
                await Task.Delay(1000, cancellationToken); // Задержка с учётом отмены
            }
        }

    }


}
