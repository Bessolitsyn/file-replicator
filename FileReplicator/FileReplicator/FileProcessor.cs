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
    ///TODO разобраться как все таки быстрее копировать файлы 
    public class FileProcessor
    {
        private readonly ConcurrentBag<(FileInfo from, FileInfo to, Exception error)> _syncErrors = [];
        private ParallelOptions _options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 2 //2 // Оптимальное значение для I/O
        };

        public delegate void CopiedFileHandler(FileInfo from, FileInfo to);
        public event CopiedFileHandler? CopiedFile;
        public delegate void CopyFailedFileHandler(FileInfo from, FileInfo to, Exception ex);
        public event CopyFailedFileHandler? CopyFailedFile;


        private event Action<FileInfo>? DeletedFile;
        private event Action<DirectoryInfo>? DeletedFolder;
        public void SetParallelOptions(ParallelOptions option)
        {
            _options = option;

        }

        public (FileInfo from, FileInfo to, Exception error)[] GetCopyErrors() => _syncErrors.ToArray();
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

        //Условие при котором файл можно копировать 
        [Obsolete]
        private static bool IsFileNeedToSync(FileInfo from, FileInfo to)
        {
            from.Refresh();
            to.Refresh();
            return from.LastAccessTime != to.LastAccessTime;
        }
        private void DeleteFile(FileInfo file)
        {
            file.Refresh();
            if (file.Exists)
            {
                file.Delete();
            }
            DeletedFile?.Invoke(file);
        }
        private void DeleteFolder(DirectoryInfo dir)
        {
            dir.Refresh();
            if (dir.Exists)
            {
                dir.Delete(true);
            }
            DeletedFolder?.Invoke(dir);
        }

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
        private void HandleErrorFile(FileInfo from, FileInfo to, Exception ex)
        {
            _syncErrors.Add((from, to, ex)); ;
            CopyFailedFile?.Invoke(from, to, ex);
        }

        ////БРЕД
        //static public void CreateSubdirsRecursive(DirectoryInfo dir)
        //{
        //    if (!dir.Exists)
        //    {
        //        dir.Create();
        //        CreateSubdirsRecursive(dir.Root);
        //    }
        //    else return;
        //}
    }
}
