using Xunit;
using Assert = Xunit.Assert;

namespace FileReplicator.Tests.Unit
{
    public class FileSynchronizerTests
    {
        //[Fact]
        //public async Task ProcessFilesAsync_WithFilesToSync_ReturnsTrue()
        //{
        //    // Arrange
        //    var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        //    var sourceDir = Path.Combine(tempRoot, "Source");
        //    var destDir = Path.Combine(tempRoot, "Dest");

        //    Directory.CreateDirectory(sourceDir);
        //    Directory.CreateDirectory(destDir);

        //    try
        //    {
        //        // Создаем тестовые файлы
        //        var file1 = Path.Combine(sourceDir, "file1.txt");
        //        var file2 = Path.Combine(sourceDir, "file2.txt");

        //        File.WriteAllText(file1, "Content 1");
        //        File.WriteAllText(file2, "Content 2");

        //        var fileSync = new FileProcessor();

        //        // Добавляем файлы для синхронизации
        //        fileSync.AddSourceFiles((new FileInfo(file1), new FileInfo(Path.Combine(destDir, "file1.txt"))));
        //        fileSync.AddSourceFiles((new FileInfo(file2), new FileInfo(Path.Combine(destDir, "file2.txt"))));

        //        // Act
        //        var result = await fileSync.ProcessFilesAsync();

        //        // Assert
        //        Assert.True(result);
        //        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
        //        Assert.True(File.Exists(Path.Combine(destDir, "file2.txt")));
        //        Assert.Equal("Content 1", File.ReadAllText(Path.Combine(destDir, "file1.txt")));
        //        Assert.Equal("Content 2", File.ReadAllText(Path.Combine(destDir, "file2.txt")));
        //    }
        //    finally
        //    {
        //        // Cleanup
        //        if (Directory.Exists(tempRoot))
        //            Directory.Delete(tempRoot, true);
        //    }
        //}

        [Fact]
        public async Task ProcessFilesAsync_WithFilesToDelete_ReturnsTrue()
        {
            // Arrange
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var destDir = Path.Combine(tempRoot, "Dest");

            Directory.CreateDirectory(destDir);

            try
            {
                // Создаем файлы для удаления
                var file1 = Path.Combine(destDir, "file1.txt");
                var file2 = Path.Combine(destDir, "file2.txt");

                File.WriteAllText(file1, "Content 1");
                File.WriteAllText(file2, "Content 2");

                var fileSync = new FileProcessor();

                // Добавляем файлы для удаления
                var filesToDelete = new[] { new FileInfo(file1), new FileInfo(file2) };
                fileSync.AddDestinationFiles(filesToDelete);

                // Act
                var result = await fileSync.ProcessFilesAsync();

                // Assert
                Assert.True(result);
                // После CopyFilesAsync файлы еще не удалены, нужно вызвать DeleteFiles
                fileSync.DeletFiles();
                Assert.False(File.Exists(file1));
                Assert.False(File.Exists(file2));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task ProcessFilesAsync_WithErrorDuringSync_AddsToErrorQueue()
        {
            // Arrange
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var sourceDir = Path.Combine(tempRoot, "Source");
            var destDir = Path.Combine(tempRoot, "Dest");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            try
            {
                // Создаем файл в source
                var sourceFile = Path.Combine(sourceDir, "file.txt");
                File.WriteAllText(sourceFile, "Content");

                // Создаем заблокированный файл в destination (симулируем ошибку)
                var destFile = Path.Combine(destDir, "file.txt");
                using (var fs = File.OpenWrite(sourceFile))
                {
                    //File.WriteAllText(sourceFile, "Locked content");

                    var fileSync = new FileProcessor();
                    fileSync.AddSourceFiles((new FileInfo(sourceFile), new FileInfo(destFile)));

                    // Act
                    var result = await fileSync.ProcessFilesAsync();

                    // Assert
                    Assert.True(result);
                    var errors = fileSync.GetSyncErrors();
                    Assert.Single(errors);
                    Assert.Contains("IOException", errors[0].error);
                }
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task ProcessFilesAsync_MixedScenario_SyncAndDelete()
        {
            // Arrange
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var sourceDir = Path.Combine(tempRoot, "Source");
            var destDir = Path.Combine(tempRoot, "Dest");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            try
            {
                // Файлы для синхронизации
                var sourceFile1 = Path.Combine(sourceDir, "sync1.txt");
                var sourceFile2 = Path.Combine(sourceDir, "sync2.txt");
                File.WriteAllText(sourceFile1, "Sync 1");
                File.WriteAllText(sourceFile2, "Sync 2");

                // Файлы для удаления
                var deleteFile1 = Path.Combine(destDir, "delete1.txt");
                var deleteFile2 = Path.Combine(destDir, "delete2.txt");
                File.WriteAllText(deleteFile1, "Delete 1");
                File.WriteAllText(deleteFile2, "Delete 2");

                var fileSync = new FileProcessor();

                // Добавляем файлы для синхронизации
                fileSync.AddSourceFiles((new FileInfo(sourceFile1), new FileInfo(Path.Combine(destDir, "sync1.txt"))));
                fileSync.AddSourceFiles((new FileInfo(sourceFile2), new FileInfo(Path.Combine(destDir, "sync2.txt"))));

                // Добавляем файлы для удаления
                fileSync.AddDestinationFiles(new[] { new FileInfo(deleteFile1), new FileInfo(deleteFile2) });

                // Act
                var syncResult = await fileSync.ProcessFilesAsync();
                fileSync.DeletFiles();

                // Assert
                Assert.True(syncResult);
                Assert.True(File.Exists(Path.Combine(destDir, "sync1.txt")));
                Assert.True(File.Exists(Path.Combine(destDir, "sync2.txt")));
                Assert.False(File.Exists(deleteFile1));
                Assert.False(File.Exists(deleteFile2));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public void ProcessFilesAsync_FileAlreadyUpToDate_NoCopyPerformed()
        {
            // Arrange
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var sourceDir = Path.Combine(tempRoot, "Source");
            var destDir = Path.Combine(tempRoot, "Dest");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            try
            {
                // Создаем одинаковые файлы в source и dest
                var sourceFile = Path.Combine(sourceDir, "file.txt");
                var destFile = Path.Combine(destDir, "file.txt");

                File.WriteAllText(sourceFile, "Same content");
                File.WriteAllText(destFile, "Same content");

                // Устанавливаем одинаковое время последнего доступа
                var lastAccessTime = DateTime.Now.AddMinutes(-10);
                File.SetLastAccessTime(sourceFile, lastAccessTime);
                File.SetLastAccessTime(destFile, lastAccessTime);

                var fileSync = new FileProcessor();
                fileSync.AddSourceFiles((new FileInfo(sourceFile), new FileInfo(destFile)));

                // Act
                var result = fileSync.ProcessFilesAsync().Result;

                // Assert
                Assert.True(result);
                // Файл не должен быть перезаписан, так как время доступа одинаковое
                Assert.Equal(lastAccessTime, File.GetLastAccessTime(destFile));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        public async Task ProcessFilesAsync_MultipleFiles_ParallelProcessing()
        {
            // Arrange
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var sourceDir = Path.Combine(tempRoot, "Source");
            var destDir = Path.Combine(tempRoot, "Dest");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            try
            {
                var fileSync = new FileProcessor();

                // Создаем 10 файлов для параллельной обработки
                for (int i = 0; i < 10; i++)
                {
                    var sourceFile = Path.Combine(sourceDir, $"file{i}.txt");
                    File.WriteAllText(sourceFile, $"Content {i}");
                    fileSync.AddSourceFiles((new FileInfo(sourceFile), new FileInfo(Path.Combine(destDir, $"file{i}.txt"))));
                }

                // Act
                var result = await fileSync.ProcessFilesAsync();

                // Assert
                Assert.True(result);
                for (int i = 0; i < 10; i++)
                {
                    var destFile = Path.Combine(destDir, $"file{i}.txt");
                    Assert.True(File.Exists(destFile));
                    Assert.Equal($"Content {i}", File.ReadAllText(destFile));
                }
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }
    }
}
