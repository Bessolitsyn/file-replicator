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
    /// <summary>
    /// Provides functionality for synchronizing files and folders between a source and destination.
    /// Handles initial replication, continuous monitoring (observation), and logging of changes.
    /// <para>Обеспечивает функциональность синхронизации файлов и папок между источником и назначением. 
    /// Обрабатывает начальную репликацию, непрерывный мониторинг (наблюдение) и логирование изменений.</para>
    /// </summary>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="FolderSync"/> class.
        /// <para>Инициализирует новый экземпляр класса <see cref="FolderSync"/>.</para>
        /// </summary>
        /// <param name="logger">Logger for recording synchronization events. / Логгер для записи событий синхронизации.</param>
        public FolderSync(ILogger logger)
        {
            _log = logger;
            //SyncedFile += OnFileSynced;
            //LogUpdated += (string message) => Console.WriteLine(message); //TODO  писать в файл
            StartedObserving += () => _isObserving = true;
            StopedObserving += () => _isObserving = false;

        }

        /// <summary>
        /// Gets the logger instance.
        /// <para>Возвращает экземпляр логгера.</para>
        /// </summary>
        public ILogger GetLog() => _log;

        /// <summary>
        /// Gets the current source and destination folders configured for synchronization.
        /// <para>Возвращает текущие исходную и целевую папки, настроенные для синхронизации.</para>
        /// </summary>
        public (DirectoryInfo source, DirectoryInfo destination) GetFolderToSync() => _foldersToSync[0];

        /// <summary>
        /// Sets the source and destination folders for synchronization. Ensures both exist.
        /// <para>Устанавливает исходную и целевую папки для синхронизации. Гарантирует, что обе существуют.</para>
        /// </summary>
        /// <param name="sourceFolder">Source directory information. / Информация об исходном каталоге.</param>
        /// <param name="destinationFolder">Destination directory information. / Информация о целевом каталоге.</param>
        public void SerFolderToSync(DirectoryInfo sourceFolder, DirectoryInfo destinationFolder)
        {
            if (!sourceFolder.Exists)
                sourceFolder.Create();
            if (!destinationFolder.Exists)
                destinationFolder.Create();
            _foldersToSync[0] = (source: sourceFolder, destination: destinationFolder);
        }

        /// <summary>
        /// Configures the system to observe changes in the source folder and reflect them in the destination.
        /// <para>Настраивает систему для наблюдения за изменениями в исходной папке и их отражения в целевой.</para>
        /// </summary>
        /// <param name="sourceFolder">Folder to monitor. / Папка для мониторинга.</param>
        /// <param name="destinationFolder">Folder to apply changes to. / Папка для применения изменений.</param>
        public void SetFolderToObserve(DirectoryInfo sourceFolder, DirectoryInfo destinationFolder)
        {
            SerFolderToSync(sourceFolder, destinationFolder);
            var fls = new FileSystemWatcher(sourceFolder.FullName);
            SetUpWatcherForFolder(fls, _isObserving);
            _observers[0] = fls;
        }

        /// <summary>
        /// Removes a folder from observation.
        /// <para>Удаляет папку из списка наблюдения.</para>
        /// </summary>
        /// <param name="sourceFolder">Source folder. / Исходная папка.</param>
        /// <param name="destinationFolder">Destination folder. / Целевая папка.</param>
        /// <exception cref="NotImplementedException">Not yet implemented. / Еще не реализовано.</exception>
        public void RemoveFolderToObserve(DirectoryInfo sourceFolder, DirectoryInfo destinationFolder)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Performs a full replication of files from source to destination for all configured folders.
        /// <para>Выполняет полную репликацию файлов из источника в назначение для всех настроенных папок.</para>
        /// </summary>
        /// <returns>A value task representing the asynchronous operation. / ValueTask, представляющий асинхронную операцию.</returns>
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

        /// <summary>
        /// Handles the event when a file copy operation fails.
        /// <para>Обрабатывает событие при сбое операции копирования файла.</para>
        /// </summary>
        /// <param name="from">Source file. / Исходный файл.</param>
        /// <param name="to">Destination file. / Целевой файл.</param>
        /// <param name="ex">The exception that occurred. / Исключение, которое произошло.</param>
        private void FileSync_CopyFailedFile(FileInfo from, FileInfo to, Exception ex)
        {

            Log(from.FullName, ex.ToString(), LogLevel.Warning); //TODO сделать елдинообразно
        }

        /// <summary>
        /// Handles the event when a file is successfully copied and updates progress tracking.
        /// <para>Обрабатывает событие при успешном копировании файла и обновляет отслеживание прогресса.</para>
        /// </summary>
        /// <param name="from">Source file. / Исходный файл.</param>
        /// <param name="to">Destination file. / Целевой файл.</param>
        private void FileSync_CopiedFile(FileInfo from, FileInfo to)
        {

            _countOfFilesToCopy--;
            if (_countOfFilesToCopy == _progressParts * (_progressPartsCount - 1))
            {

                Log("PROGRESS", $"{_progressParts * 10 - _countOfFilesToCopy} from {_progressParts * 10}", LogLevel.Information);
                _progressPartsCount--;
            }
        }

        /// <summary>
        /// Analyzes files in the source and destination to determine which files need to be copied or updated.
        /// <para>Анализирует файлы в источнике и назначении, чтобы определить, какие файлы нужно скопировать или обновить.</para>
        /// </summary>
        /// <param name="from">Source directory. / Исходный каталог.</param>
        /// <param name="to">Destination directory. / Целевой каталог.</param>
        /// <returns>A FileCopyLog containing the list of files and folders to process. / FileCopyLog, содержащий список файлов и папок для обработки.</returns>
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

        /// <summary>
        /// Obsolete: Synchronizes a queue of files.
        /// <para>Устарело: Синхронизирует очередь файлов.</para>
        /// </summary>
        /// <param name="files">Queue of source and destination files. / Очередь исходных и целевых файлов.</param>
        /// <returns>A list of files that were locked. / Список заблокированных файлов.</returns>
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

        /// <summary>
        /// Synchronizes a single file based on its existence and modification state.
        /// <para>Синхронизирует один файл на основе его существования и состояния модификации.</para>
        /// </summary>
        /// <param name="from">Source file. / Исходный файл.</param>
        /// <param name="to">Destination file (passed by reference). / Целевой файл (передается по ссылке).</param>
        /// <returns>The result code of the synchronization operation. / Код результата операции синхронизации.</returns>
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

        /// <summary>
        /// Obsolete: Synchronizes a directory.
        /// <para>Устарело: Синхронизирует каталог.</para>
        /// </summary>
        /// <param name="from">Source directory. / Исходный каталог.</param>
        /// <param name="to">Destination directory. / Целевой каталог.</param>
        /// <returns>The result code of the synchronization operation. / Код результата операции синхронизации.</returns>
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

        /// <summary>
        /// Checks if a file is currently locked by another process.
        /// <para>Проверяет, заблокирован ли файл в данный момент другим процессом.</para>
        /// </summary>
        /// <param name="file">File to check. / Файл для проверки.</param>
        /// <returns>True if the file is locked; otherwise, false. / True, если файл заблокирован; иначе false.</returns>
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

        /// <summary>
        /// Checks if two files are equivalent based on their last write time.
        /// <para>Проверяет, эквивалентны ли два файла на основе времени последнего изменения.</para>
        /// </summary>
        /// <param name="from">Source file. / Исходный файл.</param>
        /// <param name="to">Destination file. / Целевой файл.</param>
        /// <returns>True if the files are equivalent; otherwise, false. / True, если файлы эквивалентны; иначе false.</returns>
        public static bool AreFilesEquivalent(FileInfo from, FileInfo to)
        {
            return from.LastWriteTime == to.LastWriteTime;
        }

        /// <summary>
        /// Stops monitoring the folders and cleans up resources.
        /// <para>Останавливает мониторинг папок и освобождает ресурсы.</para>
        /// </summary>
        /// <returns>A task representing the asynchronous operation. / Задача, представляющая асинхронную операцию.</returns>
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
        /// Starts the folder monitoring process.
        /// <para>Запускает процесс мониторинга папок.</para>
        /// </summary>
        /// <returns>True if monitoring started successfully; otherwise, false. / True, если мониторинг успешно запущен; иначе false.</returns>
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

        /// <summary>
        /// Configures a <see cref="FileSystemWatcher"/> for a folder.
        /// <para>Настраивает <see cref="FileSystemWatcher"/> для папки.</para>
        /// </summary>
        /// <param name="watcher">The watcher instance. / Экземпляр наблюдателя.</param>
        /// <param name="startObserving">Whether to start observing immediately. / Начать ли наблюдение немедленно.</param>
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

        /// <summary>
        /// Maps a source file to its corresponding destination file.
        /// <para>Сопоставляет исходный файл с соответствующим ему целевым файлом.</para>
        /// </summary>
        /// <param name="file">The source file. / Исходный файл.</param>
        /// <returns>A tuple containing the source and destination FileInfo. / Кортеж, содержащий FileInfo источника и назначения.</returns>
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

        /// <summary>
        /// Background loop for monitoring folder activities.
        /// <para>Фоновый цикл для мониторинга активности папок.</para>
        /// </summary>
        /// <param name="cancellationToken">Token to signal cancellation. / Токен для сигнала отмены.</param>
        private async Task MonitorFolderAsync(CancellationToken cancellationToken)
        {
            //забыл зачем это 
            while (!cancellationToken.IsCancellationRequested)
            {
                // Здесь можно добавить периодическую проверку, если нужно
                await Task.Delay(1000, cancellationToken); // Задержка с учётом отмены
            }
        }

        /// <summary>
        /// Saves current synchronization logs to disk.
        /// <para>Сохраняет текущие логи синхронизации на диск.</para>
        /// </summary>
        private async Task SaveCopyLogs()
        {
            foreach (var log in _FileCopyLogInProcess)
            {
                await log.SaveCopyLogAsync();
                _log.Log(LogLevel.Debug, $"CopyLog<{log.Directory.FullName}> has been saved.");
            }
            _FileCopyLogInProcess.Clear();
        }

        /// <summary>
        /// Log helper for synchronization results.
        /// <para>Вспомогательный метод логирования результатов синхронизации.</para>
        /// </summary>
        private void Log(string from, string to, SyncOperationResultsCode code)
        {
            var logLevel = code == SyncOperationResultsCode.FailCopying || code == SyncOperationResultsCode.FailOverwriting ? LogLevel.Warning : LogLevel.Information;
            logLevel = code == SyncOperationResultsCode.FileLocked ? LogLevel.Warning : logLevel;
            logLevel = code == SyncOperationResultsCode.NoAction ? LogLevel.Debug : logLevel;
            _log.Log(logLevel, $"{LogMessages[(int)code]};{from};{to}");
            LogUpdated?.Invoke($"{logLevel}==={DateTime.Now}::{LogMessages[(int)code]};{from};{to}");
        }

        /// <summary>
        /// Log helper for single-entity synchronization events.
        /// <para>Вспомогательный метод логирования событий синхронизации одного объекта.</para>
        /// </summary>
        private void Log(string file, SyncOperationResultsCode code)
        {
            var logLevel = code == SyncOperationResultsCode.FailCopying || code == SyncOperationResultsCode.FailOverwriting ? LogLevel.Warning : LogLevel.Information;
            logLevel = code == SyncOperationResultsCode.FileLocked ? LogLevel.Warning : logLevel;
            _log.Log(logLevel, $"{LogMessages[(int)code]};{file}");
            LogUpdated?.Invoke($"{logLevel}==={DateTime.Now}::{LogMessages[(int)code]};{file}");
        }

        /// <summary>
        /// Generic log helper for custom messages.
        /// <para>Общий вспомогательный метод логирования для пользовательских сообщений.</para>
        /// </summary>
        private void Log(string message1, string message2, LogLevel logLevel)
        {
            _log.Log(logLevel, $"{message1};{message2}");
            LogUpdated?.Invoke($"{logLevel}==={DateTime.Now};{message1};{message2}");
            //_log.Enqueue($"{DateTime.Now};{LogMessages[(int)code]};{from};{to}");
        }

        #region EVENTS & AND EVENT HANDLERS

        /// <summary>
        /// Event handler for file creation in the source folder.
        /// <para>Обработчик события создания файла в исходной папке.</para>
        /// </summary>
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var file = new FileInfo(e.FullPath);
            var (from, to) = GetSourceFileInfo(file);
            var code = SyncFile(from, ref to);  //TO DO Может быть очередь сделать на обработку и обработчик будет смотреть в очередь и обрабатывать
            Log("OnFileCreated", from.Name, LogLevel.Debug);
            Log(from.FullName, to.FullName, code);
        }

        /// <summary>
        /// Event handler for file changes in the source folder.
        /// <para>Обработчик события изменения файла в исходной папке.</para>
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            //можно реализовать так если событие постоянно срабатывает часто например в течении пары секунд. можно создать задачу на синк и отправить в пул задач, а агент который читает пулл
            //имеет задержку и обнаруживает несколько задач и выполняет последнюю. распаралеливание.
            var file = new FileInfo(e.FullPath);
            //_fileInProcess.Add(file);
            var (from, to) = GetSourceFileInfo(file);
            //FileInfo dest = getSourceFileInfo(string shortfileName);
            var code = SyncFile(from, ref to);  //TO DO Может быть очередь сделать на обработку и обработчик будет смотреть в очередь и обрабатывать
            Log("OnFileChanged", from.Name, LogLevel.Debug);
            Log(from.FullName, to.FullName, code);
            //_ = _fileInProcess.TryTake(out file); //TO DO сделать обработчик если false
        }

        /// <summary>
        /// Event handler for file deletion in the source folder.
        /// <para>Обработчик события удаления файла в исходной папке.</para>
        /// </summary>
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

        /// <summary>
        /// Event handler for file or folder renaming/moving in the source folder.
        /// <para>Обработчик события переименования или перемещения файла/папки в исходной папке.</para>
        /// </summary>
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
