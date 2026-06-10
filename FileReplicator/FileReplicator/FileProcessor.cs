using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static FileReplicator.FileProcessor;
using static FileReplicator.FilesToCopy;
using static FileReplicator.ServiceMessages;

namespace FileReplicator
{
    /// <summary>
    /// Handles the low-level file operations required for synchronization, including copying, 
    /// deleting, and parallel processing of multiple files.
    /// <para>Выполняет низкоуровневые операции с файлами, необходимые для синхронизации, включая копирование, 
    /// удаление и параллельную обработку нескольких файлов.</para>
    /// </summary>
    public class FileProcessor
    {
        private readonly ConcurrentBag<(FileInfo from, FileInfo to, Exception error)> _syncErrors = [];
        private ParallelOptions _options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 2 //2 // Оптимальное значение для I/O
        };

        /// <summary>
        /// Delegate for the event triggered after a file is successfully copied.
        /// <para>Делегат для события, срабатывающего после успешного копирования файла.</para>
        /// </summary>
        public delegate void CopiedFileHandler(FileInfo from, FileInfo to);
        public event CopiedFileHandler? CopiedFile;
        
        /// <summary>
        /// Delegate for the event triggered when a file copy operation fails.
        /// <para>Делегат для события, срабатывающего при сбое операции копирования файла.</para>
        /// </summary>
        public delegate void CopyFailedFileHandler(FileInfo from, FileInfo to, Exception ex);
        public event CopyFailedFileHandler? CopyFailedFile;


        private event Action<FileInfo>? DeletedFile;
        private event Action<DirectoryInfo>? DeletedFolder;
        
        /// <summary>
        /// Sets the parallel options used for batch file copying.
        /// <para>Устанавливает параметры параллелизма, используемые для пакетного копирования файлов.</para>
        /// </summary>
        /// <param name="option">The <see cref="ParallelOptions"/> to apply. / <see cref="ParallelOptions"/> для применения.</param>
        public void SetParallelOptions(ParallelOptions option)
        {
            _options = option;

        }

        /// <summary>
        /// Returns a list of all errors encountered during the current batch of copy operations.
        /// <para>Возвращает список всех ошибок, возникших во время текущей пачки операций копирования.</para>
        /// </summary>
        /// <returns>An array of tuples containing source, destination, and the exception. / Массив кортежей, содержащих источник, назначение и исключение.</returns>
        public (FileInfo from, FileInfo to, Exception error)[] GetCopyErrors() => _syncErrors.ToArray();

        /// <summary>
        /// Asynchronously copies a collection of files in parallel.
        /// <para>Асинхронно копирует коллекцию файлов параллельно.</para>
        /// </summary>
        /// <param name="filesToCopy">A collection of <see cref="FilesToCopy"/> specifications. / Коллекция спецификаций <see cref="FilesToCopy"/>.</param>
        /// <returns>True if the operation was executed; otherwise, false if the list was empty. / True, если операция была выполнена; иначе false, если список был пуст.</returns>
        public async ValueTask<bool> CopyFilesAsync(IEnumerable<FilesToCopy> filesToCopy)
        {
            var files = filesToCopy.ToList();
            if (files.Count == 0) return false;
            int n = files.Count;

            await Parallel.ForAsync<int>(0, n, _options, (i, cancellationToken) =>
            {
                var file = files[i];
                try
                {
                    CopyFile(file.from, file.to);
                    //file.postCopiedFunc.Invoke();
                    file.postCopiedFunc.Start(); //Это может быть опасно
                }
                catch (Exception ex)
                {
                    HandleErrorFile(file.from, file.to, ex);
                    //file.postErrorFunc.Invoke();
                    file.postErrorFunc.Start();
                }
                return new ValueTask();
            });
            return true;
        }

        /// <summary>
        /// Deletes the specified files and folders from the disk.
        /// <para>Удаляет указанные файлы и папки с диска.</para>
        /// </summary>
        /// <param name="files">A collection of files to delete. / Коллекция файлов для удаления.</param>
        /// <param name="folders">A collection of folders to delete. / Коллекция папок для удаления.</param>
        /// <returns>A list of errors encountered during deletion. / Список ошибок, возникших при удалении.</returns>
        public List<(string, Exception)> DeleteFilesAndFolders(IEnumerable<FileInfo> files, IEnumerable<DirectoryInfo> folders)
        {
            List<(string, Exception)> errors = [];
            foreach (var item in files)
            {
                try
                {
                    DeleteFile(item);
                }
                catch (Exception ex)
                {
                    errors.Add((item.ToString(), ex));
                }
            }
            foreach (var item in folders)
            {
                try
                {
                    DeleteFolder(item);
                }
                catch (Exception ex)
                {
                    errors.Add((item.ToString(), ex));
                }
            }
            return errors;
        }

        /// <summary>
        /// Obsolete: Determines if a file needs to be synchronized based on access time.
        /// <para>Устарело: Определяет, нужно ли синхронизировать файл на основе времени доступа.</para>
        /// </summary>
        [Obsolete]
        private static bool IsFileNeedToSync(FileInfo from, FileInfo to)
        {
            from.Refresh();
            to.Refresh();
            return from.LastAccessTime != to.LastAccessTime;
        }

        /// <summary>
        /// Deletes a specific file from the disk.
        /// <para>Удаляет конкретный файл с диска.</para>
        /// </summary>
        /// <param name="file">The file to delete. / Файл для удаления.</param>
        private void DeleteFile(FileInfo file)
        {
            file.Refresh();
            if (file.Exists)
            {
                file.Delete();
            }
            DeletedFile?.Invoke(file);
        }

        /// <summary>
        /// Deletes a specific directory and its contents recursively.
        /// <para>Удаляет конкретный каталог и его содержимое рекурсивно.</para>
        /// </summary>
        /// <param name="dir">The directory to delete. / Каталог для удаления.</param>
        private void DeleteFolder(DirectoryInfo dir)
        {
            dir.Refresh();
            if (dir.Exists)
            {
                dir.Delete(true);
            }
            DeletedFolder?.Invoke(dir);
        }

        /// <summary>
        /// Performs the actual copying of a file from source to destination.
        /// <para>Выполняет фактическое копирование файла из источника в назначение.</para>
        /// </summary>
        /// <param name="from">The source file. / Исходный файл.</param>
        /// <param name="to">The destination file. / Целевой файл.</param>
        private void CopyFile(FileInfo from, FileInfo to)
        {
            to.Refresh();
            if (!to.Directory.Exists) to.Directory.Create();
            if (to.Exists)
            {
                from.CopyTo(to.FullName, true);
            }
            else
            {
                from.CopyTo(to.FullName);
            }
            CopiedFile?.Invoke(from, to);

        }

        /// <summary>
        /// Records an error encountered during a file operation and triggers the failure event.
        /// <para>Записывает ошибку, возникшую при операции с файлом, и вызывает событие сбоя.</para>
        /// </summary>
        /// <param name="from">Source file. / Исходный файл.</param>
        /// <param name="to">Destination file. / Целевой файл.</param>
        /// <param name="ex">The exception thrown. / Выброшенное исключение.</param>
        private void HandleErrorFile(FileInfo from, FileInfo to, Exception ex)
        {
            _syncErrors.Add((from, to, ex)); ;
            CopyFailedFile?.Invoke(from, to, ex);
        }
    }
}
