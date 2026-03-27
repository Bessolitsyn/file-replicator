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
using Microsoft.Extensions.Logging;
using System.Security;
using System.Collections.Concurrent;
using static FileReplicator.FileCopyLog;

[assembly: InternalsVisibleTo("FolderSyncTests")]

namespace FileReplicator
{
    public class FolderSync
    {
        private readonly ILogger _log;
        private readonly ConcurrentBag<FileInfo> _fileInProcess = [];
        private readonly List<FileCopyLog> _FileCopyLogInProcess = [];
        private readonly (DirectoryInfo source, DirectoryInfo destination)[] _foldersToSync = new (DirectoryInfo source, DirectoryInfo destination)[1];
        private FileSystemWatcher[] _observers = new FileSystemWatcher[1];
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _observersTask;
        private bool _isObserving = false;
        private int _countOfFilesToCopy = 0;
        private int _progressParts = 0;
        private int _progressPartsCount = 10;
        //private readonly ISyncedFileList _previosSyncedFileList; //_previosSyncedFolderList?

        public bool HaveFilesInProcess { get => _fileInProcess.Count != 0; }
        public bool IsObserving { get => _isObserving; }

        public FolderSync(ILogger logger)
        {
            _log = logger;
            //SyncedFile += OnFileSynced;
            //LogUpdated += (string message) => Console.WriteLine(message); //TODO  писать в файл
            StartedObserving += () => _isObserving = true;
            StopedObserving += () => _isObserving = false;

        }

        public ILogger GetLog() => _log;
        public (DirectoryInfo source, DirectoryInfo destination) GetFolderToSync() => _foldersToSync[0];

        /// <summary>
        /// Добавить папку для синхронизации. Если в destinationFolder не существует папки одноименной с папкой-источником, таковая будет создана
        /// </summary>
        /// <param name="sourceFolder"></param>
        /// <param name="destinationFolder"></param>
        public void SerFolderToSync(DirectoryInfo sourceFolder, DirectoryInfo destinationFolder)
        {
            if (!sourceFolder.Exists)
                sourceFolder.Create();
            if (!destinationFolder.Exists)
                destinationFolder.Create();
            _foldersToSync[0] = (source: sourceFolder, destination: destinationFolder);
        }

        public void SetFolderToObserve(DirectoryInfo sourceFolder, DirectoryInfo destinationFolder)
        {
            SerFolderToSync(sourceFolder, destinationFolder);
            var fls = new FileSystemWatcher(sourceFolder.FullName);
            SetUpWatcherForFolder(fls, _isObserving);
            _observers[0] = fls;
        }
        public void RemoveFolderToObserve(DirectoryInfo sourceFolder, DirectoryInfo destinationFolder)
        {
            throw new NotImplementedException();
        }

        public async ValueTask ReplicateAsync()
        {
            foreach (var f in _foldersToSync)
            {
                Log($"FOLDER SYNC START AT {DateTime.Now}", f.source.FullName + " => " + f.destination.FullName, LogLevel.Information);

                var fileCopyLog = await GetFileCopyLogAsync(f.source, f.destination);
                var fileSync = new FileProcessor();
                fileSync.SetParallelOptions(new ParallelOptions()
                {
                    //Тут надо экспериментировать на SSD с учетом антивируса на моем ноуте это самое оптимальное 
                    //TODO Проверить сеть, и другие случаи.
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });

                _countOfFilesToCopy = fileCopyLog.GetFilesToCopyCount();
                _progressParts = _countOfFilesToCopy / 10;
                fileSync.CopiedFile += FileSync_CopiedFile;
                if (_countOfFilesToCopy > 100) fileSync.CopyFailedFile += FileSync_CopyFailedFile;

                _ = await fileSync.CopyFilesAsync(fileCopyLog.GetFilesToCopy());
                _ = fileSync.GetCopyErrors();

                _ = fileSync.DeleteFilesAndFolders(
                    fileCopyLog.GetFilesToDelete(),
                    fileCopyLog.GetFoldersToDelete()
                );

                await SaveCopyLogs();

                Log("FOLDER SYNC STOP ", f.source.FullName + " => " + f.destination.FullName, LogLevel.Information);

            }

        }

        private void FileSync_CopyFailedFile(FileInfo from, FileInfo to, Exception ex)
        {

            Log(from.FullName, ex.ToString(), LogLevel.Warning); //TODO сделать елдинообразно
        }
        //TODO сделать потоко безопасными
        private void FileSync_CopiedFile(FileInfo from, FileInfo to)
        {

            _countOfFilesToCopy--;
            if (_countOfFilesToCopy == _progressParts * (_progressPartsCount - 1))
            {

                Log("PROGRESS", $"{_progressParts * 10 - _countOfFilesToCopy} from {_progressParts * 10}", LogLevel.Information);
                _progressPartsCount--;
            }
        }

        // TODO: Асинхронность
        //Дает список файлов для копирования удаления и загружает предыдущий лог синхронизации
        public async Task<FileCopyLog> GetFileCopyLogAsync(DirectoryInfo from, DirectoryInfo to)
        {
            var copyLog = new FileCopyLog(to);
            var _ = await copyLog.ReadAsync();
            _log.Log(LogLevel.Debug, $"CopyLog<{copyLog.Directory.FullName}> has been read");
            _FileCopyLogInProcess.Add(copyLog);

            //Сбор файлов обработки
            foreach (var file in from.GetFiles())
            {
                var answ = copyLog.CheckFile(file);
                if (answ == CheckFileResult.New || answ == CheckFileResult.Updated)
                {
                    Action act1 = () =>
                    {
                        copyLog.AddFile(file);
                    };
                    var t1 = new Task(act1);
                    Action act2 = () =>
                    {
                        copyLog.AddFileWithError(file);
                    };
                    var t2 = new Task(act2);
                    var filetocopy = new FilesToCopy(
                        from: file,
                        to: new FileInfo(Path.Combine(to.FullName, file.Name)),
                        postCopiedFunc: t1,
                        postErrorFunc: t2
                    );
                    copyLog.AddFileToCopy(filetocopy);
                }
            }


            // Синхронизация папок (рекурсивно)
            DirectoryInfo[] subDirs = from.GetDirectories();
            foreach (var subDir in subDirs)
            {
                copyLog.AddDir(subDir);
                var targetSubDir = new DirectoryInfo(Path.Combine(to.FullName, subDir.Name));
                var subDirLog = await GetFileCopyLogAsync(subDir, targetSubDir);
                copyLog.AddSubDirLog(subDirLog);
            }
            return copyLog;

        }

        [Obsolete]
        public IEnumerable<(FileInfo from, FileInfo to)> SyncFiles(Queue<(FileInfo from, FileInfo to)> files)
        {
            List<(FileInfo from, FileInfo to)> lockedFiles = [];
            while (files.Count > 0)
            {
                var file = files.Dequeue();
                var code = SyncFile(file.from, ref file.to);
                if (code == SyncOperationResultsCode.FileLocked)
                {
                    lockedFiles.Add(file);
                }

            }

            return lockedFiles;
        }

        //Эта функция вызывается при мониторинге папки 
        //TODO сделать запись в журнал о том что файл скопирован
        public SyncOperationResultsCode SyncFile(FileInfo from, ref FileInfo to)
        {
            //добавить проверку что файл только что был скопирован, а то событие вызывающее это метод происходит
            //несколько раз и файл можт быть уже обработан и возникают дублирующие сообщения.
            from.Refresh();
            to.Refresh();
            SyncOperationResultsCode code = SyncOperationResultsCode.NoAction;
            if (!IsFileLocked(from))
            {

                if (from.Exists && to.Exists)
                {

                    if (!AreFilesEquivalent(from, to))
                    {
                        from.CopyTo(to.FullName, true);
                        code = SyncOperationResultsCode.SuccessfulOverwriting;
                    }

                }
                if (from.Exists && !to.Exists)
                {
                    if (!to.Directory.Exists) to.Directory.Create();
                    from.CopyTo(to.FullName);
                    code = SyncOperationResultsCode.SuccessfulCopying;
                }
                if (!from.Exists && to.Exists)
                {
                    to.Delete();
                    code = SyncOperationResultsCode.SuccessfulDeleting;
                }
            }
            else
                code = SyncOperationResultsCode.FileLocked;
            to.Refresh();
            SyncedFile?.Invoke((code, from, to));
            return code;
        }
        [Obsolete]
        public SyncOperationResultsCode SyncDir(DirectoryInfo from, ref DirectoryInfo to)
        {
            //добавить проверку что файл только что был скопирован, а то событие вызывающее это метод происходит
            //несколько раз и файл можт быть уже обработан и возникают дублирующие сообщения.
            from.Refresh();
            to.Refresh();
            SyncOperationResultsCode code = SyncOperationResultsCode.NoAction;
            if (!from.Exists && !to.Exists)
            {

            }

            to.Refresh();
            return code;
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
        /// Остановка мониторинга папок
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StopObservingAsync()
        {
            var result = false;
            Log("StopObserving", "Execute", LogLevel.Information);
            if (_observersTask != null)
            {
                _cancellationTokenSource.Cancel(); // Отправляем сигнал отмены
                _observers[0].EnableRaisingEvents = false;

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
                    _observers[0].Dispose();
                    _cancellationTokenSource.Dispose();
                }
            }
            StopedObserving?.Invoke();
            result = true;
            return result;
        }
        /// <summary>
        /// Запуск мониторинга папок
        /// </summary>
        public bool StartObserving()
        {
            if (_observers[0] == null)
            {
                Log("StartObserving", "Fail, No folder to observe", LogLevel.Information);
                return false;
            }
            Log("StartObserving", "Execute", LogLevel.Information);
            _observers[0].EnableRaisingEvents = true;
            // Запускаем асинхронный мониторинг с возможностью отмены
            _observersTask = Task.Run(() => MonitorFolderAsync(_cancellationTokenSource.Token));
            StartedObserving?.Invoke();
            return true;
        }

        public void SetUpWatcherForFolder(FileSystemWatcher watcher, bool startObserving = false)
        {
            var o = watcher;
            o.NotifyFilter = NotifyFilters.LastWrite
                                  | NotifyFilters.FileName
                                  | NotifyFilters.DirectoryName;

            o.Changed += OnFileChanged;
            o.Created += OnFileCreated;
            o.Deleted += OnFileDeleted;
            o.Renamed += OnFileRenamed;
            o.EnableRaisingEvents = startObserving;
            o.IncludeSubdirectories = true;

            // Запускаем асинхронный мониторинг с возможностью отмены
            _observersTask = Task.Run(() => MonitorFolderAsync(_cancellationTokenSource.Token));
            StartedObserving?.Invoke();
        }

        public override string ToString()
        {
            return $"{String.Join(";", _foldersToSync.Select(f => f.source.FullName))}";
        }

        private (FileInfo from, FileInfo to) GetSourceFileInfo(FileInfo file)
        {
            if (_foldersToSync[0].source.FullName == file.Directory?.FullName)
            {
                return (file, new FileInfo(Path.Combine(_foldersToSync[0].destination.FullName, file.Name)));
            }
            else
            {
                var sb = new StringBuilder(file.FullName).Replace(_foldersToSync[0].source.FullName, null);
                var to = new FileInfo(Path.Join(_foldersToSync[0].destination.FullName, sb.ToString()));
                return (file, to);
            }
        }

        private async Task MonitorFolderAsync(CancellationToken cancellationToken)
        {
            //забыл зачем это 
            while (!cancellationToken.IsCancellationRequested)
            {
                // Здесь можно добавить периодическую проверку, если нужно
                await Task.Delay(1000, cancellationToken); // Задержка с учётом отмены
            }
        }

        private async Task SaveCopyLogs()
        {
            foreach (var log in _FileCopyLogInProcess)
            {
                await log.SaveCopyLogAsync();
                _log.Log(LogLevel.Debug, $"CopyLog<{log.Directory.FullName}> has been saved.");
            }
            _FileCopyLogInProcess.Clear();
        }

        private void Log(string from, string to, SyncOperationResultsCode code)
        {
            var logLevel = code == SyncOperationResultsCode.FailCopying || code == SyncOperationResultsCode.FailOverwriting ? LogLevel.Warning : LogLevel.Information;
            logLevel = code == SyncOperationResultsCode.FileLocked ? LogLevel.Warning : logLevel;
            logLevel = code == SyncOperationResultsCode.NoAction ? LogLevel.Debug : logLevel;
            _log.Log(logLevel, $"{LogMessages[(int)code]};{from};{to}");
            LogUpdated?.Invoke($"{logLevel}==={DateTime.Now}::{LogMessages[(int)code]};{from};{to}");
        }
        private void Log(string file, SyncOperationResultsCode code)
        {
            var logLevel = code == SyncOperationResultsCode.FailCopying || code == SyncOperationResultsCode.FailOverwriting ? LogLevel.Warning : LogLevel.Information;
            logLevel = code == SyncOperationResultsCode.FileLocked ? LogLevel.Warning : logLevel;
            _log.Log(logLevel, $"{LogMessages[(int)code]};{file}");
            LogUpdated?.Invoke($"{logLevel}==={DateTime.Now}::{LogMessages[(int)code]};{file}");
        }
        private void Log(string message1, string message2, LogLevel logLevel)
        {
            _log.Log(logLevel, $"{message1};{message2}");
            LogUpdated?.Invoke($"{logLevel}==={DateTime.Now};{message1};{message2}");
            //_log.Enqueue($"{DateTime.Now};{LogMessages[(int)code]};{from};{to}");
        }

        #region EVENTS & AND EVENT HANDLERS
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var file = new FileInfo(e.FullPath);
            var (from, to) = GetSourceFileInfo(file);
            var code = SyncFile(from, ref to);  //TO DO Может быть очередь сделать на обработку и обработчик будет смотреть в очередь и ее обрабатывать
            Log("OnFileCreated", from.Name, LogLevel.Debug);
            Log(from.FullName, to.FullName, code);
        }
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            //можно реализовать так если событие постоянно срабатывает часто например в течении пары секунд. можно создать задачу на синк и отправить в пул задач, а агент который читает пулл
            //имеет задержку и обнаруживает несколько задач и выполняет последнюю. распаралеливание.
            var file = new FileInfo(e.FullPath);
            //_fileInProcess.Add(file);
            var (from, to) = GetSourceFileInfo(file);
            //FileInfo dest = getSourceFileInfo(string shortfileName);
            var code = SyncFile(from, ref to);  //TO DO Может быть очередь сделать на обработку и обработчик будет смотреть в очередь и ее обрабатывать
            Log("OnFileChanged", from.Name, LogLevel.Debug);
            Log(from.FullName, to.FullName, code);
            //_ = _fileInProcess.TryTake(out file); //TO DO сделать обработчик если false
        }
        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            var file = new FileInfo(e.FullPath);
            //_fileInProcess.Add(file);
            var (from, to) = GetSourceFileInfo(file);
            var code = SyncFile(from, ref to);
            Log("OnFileDeleted", from.Name, LogLevel.Debug);
            Log(from.FullName, to.FullName, code);
            //_fileInProcess.TryTake(out file);
        }
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            var code = e.Name == e.OldName ? SyncOperationResultsCode.SuccessfulMoved : SyncOperationResultsCode.SuccessfulRenamed;
            var isDir = !File.Exists(e.FullPath);
            //{
            var file = new FileInfo(e.FullPath);
            var oldfile = new FileInfo(e.OldFullPath);

            var (from, to) = GetSourceFileInfo(file);
            var (fromOld, toOld) = GetSourceFileInfo(oldfile);
            if (isDir)
            {
                var olddir= new DirectoryInfo(toOld.FullName);
                if (olddir.Exists)
                {
                    olddir.MoveTo(to.FullName);
                }
                else
                {
                    //TO DO Копировать папку с файлами из источника в получатель -
                    //папка может быть большой и вообще могут быть конфликты
                    //пока сделаю стандартно череp копирование 
                    //var fromdir = new DirectoryInfo(from.FullName);
                    //var todir = new DirectoryInfo(to.FullName);
                    var fc = new FolderSync(_log);
                    fc.SerFolderToSync(new DirectoryInfo(from.FullName), new DirectoryInfo(to.FullName));
                    var t = Task.Run(()=>fc.ReplicateAsync());
                    Log("OnFileRenamed", from.Name, LogLevel.Debug);
                    Log($"Copy folder {from.FullName} to {to.FullName} in background", to.FullName, LogLevel.Information);
                    return;
                }
            }
            else if (toOld.Exists)
            {
                toOld.MoveTo(to.FullName);
            }
            else
            {
                code = SyncFile(from, ref to);
            }

            //var (from, to) = GetSourceFileInfo(file);              
            //var code = SyncFile(from, ref to);
            //var (fromOld, toOld) = GetSourceFileInfo(oldfile);
            //var code_ = SyncFile(fromOld, ref toOld);

            Log("OnFileRenamed", from.Name, LogLevel.Debug);
            Log(toOld.FullName, to.FullName, code);
            //}
            //else
            //{
            //    //переименовывание или перемещение директории
            //    var dir = new DirectoryInfo(e.FullPath);
            //    var file = new FileInfo(e.FullPath);
            //    var oldfile = new FileInfo(e.OldFullPath);
            //    var (from, to) = GetSourceFileInfo(file);
            //    var (fromOld, toOld) = GetSourceFileInfo(oldfile);
            //    toOld.MoveTo(to.FullName);
            //    Log("OnFileRenamed", from.Name, LogLevel.Debug);
            //    Log(from.FullName, to.FullName, code);
            //    Log(fromOld.FullName, toOld.FullName, code_);
            //}
        }

        public delegate void LogUpdatedHandler(string message);
        public event LogUpdatedHandler? LogUpdated;

        public delegate void SyncedFileHandler((SyncOperationResultsCode code, FileInfo from, FileInfo to) syncInfo);
        public event SyncedFileHandler? SyncedFile;

        private event Action StartedObserving;
        private event Action StopedObserving;

        #endregion
    }


}
