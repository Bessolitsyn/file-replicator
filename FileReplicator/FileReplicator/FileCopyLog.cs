using Microsoft.Extensions.Logging;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Xml.Linq;
using static FileReplicator.FileCopyLog;
using static FileReplicator.FilesToCopy;
using static FileReplicator.ServiceMessages;

namespace FileReplicator
{
    /// <summary>
    /// Manages the synchronization log for a directory, tracking which files have been copied 
    /// and identifying files that need updating or deletion.
    /// <para>Управляет логом синхронизации для каталога, отслеживая, какие файлы были скопированы, 
    /// и определяя файлы, которые требуют обновления или удаления.</para>
    /// </summary>
    public class FileCopyLog
    {
        public delegate void PostFunc();

        private readonly ConcurrentDictionary<string, long> _previosCopyLog = [];
        private readonly ConcurrentDictionary<string, long> _currentCopyLog = [];
        private readonly FileInfo _fileOfLog;
        private readonly DirectoryInfo _currentDir;
        private readonly List<FileCopyLog> _subDirsLogs = [];
        private readonly List<FileInfo> _filesToDelete = [];
        private List<DirectoryInfo> _foldersToDelete = [];
        private List<FilesToCopy> _filesToCopy = [];
        public DirectoryInfo Directory { get => _currentDir; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileCopyLog"/> class. 
        /// Creates a hidden system directory '_reptorlog' to store the log file.
        /// <para>Инициализирует новый экземпляр класса <see cref="FileCopyLog"/>. 
        /// Создает скрытый системный каталог '_reptorlog' для хранения файла лога.</para>
        /// </summary>
        /// <param name="currentDir">The directory being synchronized. / Синхронизируемый каталог.</param>
        public FileCopyLog(DirectoryInfo currentDir)
        {
            var logDir = new DirectoryInfo(Path.Combine(currentDir.FullName, "_reptorlog"));
            if (!logDir.Exists)
            {
                logDir.Create();
                logDir.Attributes = FileAttributes.Hidden;
                logDir.Attributes = FileAttributes.System;
            }
            _fileOfLog = new FileInfo(Path.Combine(logDir.FullName, "files.list"));
            _currentDir = currentDir;
        }

        /// <summary>
        /// Adds a file to the list of files to be copied.
        /// <para>Добавляет файл в список файлов для копирования.</para>
        /// </summary>
        /// <param name="file">The file specification to copy. / Спецификация файла для копирования.</param>
        public void AddFileToCopy(FilesToCopy file)
        {
            _filesToCopy.Add(file);
        }

        /// <summary>
        /// Marks a file as successfully processed in the current synchronization cycle.
        /// <para>Помечает файл как успешно обработанный в текущем цикле синхронизации.</para>
        /// </summary>
        /// <param name="file">The file to record. / Файл для записи.</param>
        public void AddFile(FileInfo file)
        {
            _currentCopyLog[file.Name] = file.Length;
            if (_previosCopyLog.ContainsKey(file.Name)) _previosCopyLog.Remove(file.Name, out _);
        }

        /// <summary>
        /// Marks a directory as processed in the current synchronization cycle.
        /// <para>Помечает каталог как обработанный в текущем цикле синхронизации.</para>
        /// </summary>
        /// <param name="dir">The directory to record. / Каталог для записи.</param>
        public void AddDir(DirectoryInfo dir)
        {
            var dirStr = $"[DIR]{dir.Name}";
            _currentCopyLog[dirStr] = 0;
            if (_previosCopyLog.ContainsKey(dirStr)) _previosCopyLog.Remove(dirStr, out _);
        }

        /// <summary>
        /// Marks a file as having encountered an error during processing.
        /// <para>Помечает файл как имеющий ошибку при обработке.</para>
        /// </summary>
        /// <param name="file">The file that caused the error. / Файл, вызвавший ошибку.</param>
        public void AddFileWithError(FileInfo file)
        {
            _currentCopyLog[file.Name] = -1;
            if (_previosCopyLog.ContainsKey(file.Name)) _previosCopyLog.Remove(file.Name, out _);
        }

        /// <summary>
        /// Adds a log from a subdirectory to the current log hierarchy.
        /// <para>Добавляет лог из подкаталога в текущую иерархию логов.</para>
        /// </summary>
        /// <param name="log">The subdirectory log instance. / Экземпляр лога подкаталога.</param>
        public void AddSubDirLog(FileCopyLog log)
        {
            _subDirsLogs.Add(log);
        }

        /// <summary>
        /// Retrieves a comprehensive list of all files to be copied, including those from subdirectories.
        /// <para>Возвращает полный список всех файлов для копирования, включая файлы из подкаталогов.</para>
        /// </summary>
        /// <returns>A collection of <see cref="FilesToCopy"/>. / Коллекция <see cref="FilesToCopy"/>.</returns>
        public IEnumerable<FilesToCopy> GetFilesToCopy()
        {
            var filesToCopy = new List<FilesToCopy>();
            filesToCopy.AddRange(_filesToCopy);
            _subDirsLogs.ForEach(l => filesToCopy.AddRange(l.GetFilesToCopy()));
            return filesToCopy;
        }

        /// <summary>
        /// Calculates the total number of files that need to be copied across all directories in the log.
        /// <para>Вычисляет общее количество файлов, которые необходимо скопировать во всех каталогах лога.</para>
        /// </summary>
        /// <returns>The total count of files to copy. / Общее количество файлов для копирования.</returns>
        public int GetFilesToCopyCount()
        {
            var count = 0;
            count += _filesToCopy.Count;
            _subDirsLogs.ForEach(l => count += l.GetFilesToCopyCount());
            return count;
        }

        /// <summary>
        /// Determines which files should be deleted from the destination because they no longer exist in the source.
        /// <para>Определяет, какие файлы должны быть удалены из назначения, так как они больше не существуют в источнике.</para>
        /// </summary>
        /// <returns>A collection of <see cref="FileInfo"/> representing files to delete. / Коллекция <see cref="FileInfo"/>, представляющая файлы для удаления.</returns>
        public IEnumerable<FileInfo> GetFilesToDelete()
        {
            if (_filesToDelete.Count == 0)
            {
                _subDirsLogs.ForEach(l => _filesToDelete.AddRange(l.GetFilesToDelete()));
                _filesToDelete.AddRange(_previosCopyLog
                    .Where(k => !k.Key.Contains("[DIR]"))
                    .Select(vp => new FileInfo(Path.Combine(_currentDir.FullName, vp.Key)))
                );
            }
            return _filesToDelete;
        }

        /// <summary>
        /// Determines which directories should be deleted from the destination because they no longer exist in the source.
        /// <para>Определяет, какие каталоги должны быть удалены из назначения, так как они больше не существуют в источнике.</para>
        /// </summary>
        /// <returns>A collection of <see cref="DirectoryInfo"/> representing folders to delete. / Коллекция <see cref="DirectoryInfo"/>, представляющих папки для удаления.</returns>
        public IEnumerable<DirectoryInfo> GetFoldersToDelete()
        {
            if (_foldersToDelete.Count == 0)
            {
                _subDirsLogs.ForEach(l => _foldersToDelete.AddRange(l.GetFoldersToDelete()));
                _foldersToDelete.AddRange(_previosCopyLog
                    .Where(k => k.Key.Contains("[DIR]"))
                    .Select(vp => new DirectoryInfo(Path.Combine(_currentDir.FullName, vp.Key.Substring(5))))
                );
            }
            return _foldersToDelete;
        }
        
        /// <summary>
        /// Checks a file against the previous log to see if it's new, unchanged, or updated.
        /// <para>Сравнивает файл с предыдущим логом, чтобы определить, является ли он новым, неизмененным или обновленным.</para>
        /// </summary>
        /// <param name="file">The file to check. / Файл для проверки.</param>
        /// <returns>A <see cref="CheckFileResult"/> indicating the file's status. / <see cref="CheckFileResult"/>, указывающий статус файла.</returns>
        public CheckFileResult CheckFile(FileInfo file)
        {
            CheckFileResult res;
            if (!_previosCopyLog.ContainsKey(file.Name))
            {
                res = CheckFileResult.New;
            }
            else
            {
                if (_previosCopyLog[file.Name] == file.Length)
                {
                    _currentCopyLog[file.Name] = _previosCopyLog[file.Name];
                    res = CheckFileResult.Same;
                }
                else
                {
                    res = CheckFileResult.Updated;
                }
                _previosCopyLog.Remove(file.Name, out _);
            }
            return res;
        }

        /// <summary>
        /// Identifies files that exist in the destination but are missing from the previous log.
        /// <para>Идентифицирует файлы, которые существуют в назначении, но отсутствуют в предыдущем логе.</para>
        /// </summary>
        /// <returns>A collection of filenames that were not found. / Коллекция имен файлов, которые не были найдены.</returns>
        public IEnumerable<string> GetNotExistedFiles()
        {
            return _previosCopyLog.Select(f => new FileInfo(Path.Combine(_currentDir.FullName, f.Key))).Where(f => f.Exists).Select(f => f.Name);
        }

        /// <summary>
        /// Asynchronously saves the current synchronization state to the log file on disk.
        /// <para>Асинхронно сохраняет текущее состояние синхронизации в файл лога на диске.</para>
        /// </summary>
        /// <returns>A task representing the asynchronous operation. / Задача, представляющая асинхронную операцию.</returns>
        public async Task SaveCopyLogAsync()
        {
            if (_fileOfLog.Exists) _fileOfLog.Delete();
            //using var strmW = _fileOfLog.CreateText();
            using var strmW = new StreamWriter(_fileOfLog.Create(), Encoding.UTF8);
            _fileOfLog.Attributes = FileAttributes.Hidden;
            foreach (var file in _currentCopyLog)
            {
                await strmW.WriteLineAsync($"{file.Key};{file.Value}");
            }
            strmW.Close();

        }

        /// <summary>
        /// Asynchronously reads the previous synchronization state from the log file.
        /// <para>Асинхронно считывает предыдущее состояние синхронизации из файла лога.</para>
        /// </summary>
        /// <returns>A task returning the number of files read from the log. / Задача, возвращающая количество файлов, прочитанных из лога.</returns>
        public async Task<int> ReadAsync()
        {
            if (_fileOfLog.Exists)
            {
                using var strmR = new StreamReader(_fileOfLog.OpenRead(), Encoding.UTF8);
                while (!strmR.EndOfStream)
                {
                    var line = (await strmR.ReadLineAsync())?.Split(";");
                    if (line?.Length == 2)
                        _previosCopyLog[line[0]] = long.Parse(line[1]);
                }
                strmR.Close();
            }
            return _previosCopyLog.Count;
            //else
            //    throw new FileNotFoundException($"{_fileOfLog.FullName}");
        }
    }

    /// <summary>
    /// Represents the result of checking if a file needs to be synchronized.
    /// <para>Представляет результат проверки того, нужно ли синхронизировать файл.</para>
    /// </summary>
    public enum CheckFileResult
    {
        /// <summary> File is new. / Файл новый. </summary>
        New,
        /// <summary> File is unchanged. / Файл не изменился. </summary>
        Same,
        /// <summary> File has been updated. / Файл был обновлен. </summary>
        Updated
    }

    /// <summary>
    /// A record representing a file and its target destination, along with tasks to execute after success or failure.
    /// <para>Запись, представляющая файл и его целевое назначение, а также задачи для выполнения после успеха или сбоя.</para>
    /// </summary>
    public record FilesToCopy(FileInfo from, FileInfo to, Task postCopiedFunc, Task postErrorFunc);

}
