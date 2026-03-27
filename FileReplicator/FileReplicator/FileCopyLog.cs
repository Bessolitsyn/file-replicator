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
    public class FileCopyLog//TODO должен работать и с папками
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
        public void AddFileToCopy(FilesToCopy file)
        {
            _filesToCopy.Add(file);
        }
        public void AddFile(FileInfo file)
        {
            _currentCopyLog[file.Name] = file.Length;
            if (_previosCopyLog.ContainsKey(file.Name)) _previosCopyLog.Remove(file.Name, out _);
        }
        public void AddDir(DirectoryInfo dir)
        {
            var dirStr = $"[DIR]{dir.Name}";
            _currentCopyLog[dirStr] = 0;
            if (_previosCopyLog.ContainsKey(dirStr)) _previosCopyLog.Remove(dirStr, out _);
        }
        public void AddFileWithError(FileInfo file)
        {
            _currentCopyLog[file.Name] = -1;
            if (_previosCopyLog.ContainsKey(file.Name)) _previosCopyLog.Remove(file.Name, out _);
        }
        public void AddSubDirLog(FileCopyLog log)
        {
            _subDirsLogs.Add(log);
        }

        public IEnumerable<FilesToCopy> GetFilesToCopy()
        {
            var filesToCopy = new List<FilesToCopy>();
            filesToCopy.AddRange(_filesToCopy);
            _subDirsLogs.ForEach(l => filesToCopy.AddRange(l.GetFilesToCopy()));
            return filesToCopy;
        }
        public int GetFilesToCopyCount()
        {
            var count = 0;
            count += _filesToCopy.Count;
            _subDirsLogs.ForEach(l => count += l.GetFilesToCopyCount());
            return count;
        }
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

        public IEnumerable<string> GetNotExistedFiles()
        {
            return _previosCopyLog.Select(f => new FileInfo(Path.Combine(_currentDir.FullName, f.Key))).Where(f => f.Exists).Select(f => f.Name);
        }

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
    public enum CheckFileResult
    {
        New,
        Same,
        Updated
    }

    public record FilesToCopy(FileInfo from, FileInfo to, Task postCopiedFunc, Task postErrorFunc);

}
