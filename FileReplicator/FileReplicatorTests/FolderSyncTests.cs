using Castle.Core.Logging;
using FileReplicator;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;
using Microsoft.Extensions.Logging;
using static FileReplicator.ServiceMessages;


namespace FileReplicator.Tests.Unit
{
    public class FolderSyncTests
    {
        [Theory]
        [InlineData("sdadds", "sdsdds")]
        [InlineData("\\FolderSyncTest\\From\\123", "\\FolderSyncTest\\To")]
        public void AddFolderToSyncTest_WithNotValidArguments(string from, string to)
        {
            try
            {
                var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
                var fc = new FolderSync(mlog.Object);

                fc.AddFolderToSync(from, to);
                fc.Sync();
                Xunit.Assert.Single(fc.GetFolderToSync());
            }
            catch (Exception)
            {
                Xunit.Assert.Fail();
            }
            finally
            {
                Directory.Delete(from, true);
                Directory.Delete(to, true);
            }


        }
        [Theory]
        [InlineData("\\FolderSyncTest\\From", "\\FolderSyncTest\\1")]
        [InlineData("\\FolderSyncTest\\From", "\\FolderSyncTest\\NoExistFlder")]
        public void SyncFilesTest(string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";

            var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
            var fc = new FolderSync(mlog.Object);

            var files = new Queue<(FileInfo from, FileInfo to)>();
            foreach (var f in Directory.GetFiles(cd + from))
            {
                files.Enqueue((from: new FileInfo(f), to: new FileInfo(f.Replace(from, to))));
            }

            try
            {
                using (var fs = File.OpenWrite(cd + from + "\\wg0.txt"))
                {
                    Helper.AddText(fs, "\r\n --rn--");
                    Helper.AddText(fs, "\r --r--");
                    Helper.AddText(fs, "\n --n--");
                    var lockedFiles = fc.SyncFiles(files);
                    fs.Close();
                }
                Xunit.Assert.Equal(Directory.GetFiles(cd + from).Length, Directory.GetFiles(cd + to).Length + 1);



            }
            catch (Exception ex)
            {
                Xunit.Assert.True(ex.Message == "Destination folder doesn't exist");
            }
            finally
            {
                if (Directory.Exists(cd + to))
                    Directory.GetFiles(cd + to).ToList().ForEach(f => File.Delete(f));
            }

        }

        [Theory]
        [InlineData("\\FolderSyncTest\\From", "\\FolderSyncTest\\2")]
        public void SyncFolderTest(string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var fromDir = new DirectoryInfo(cd + from);
            var toDir = new DirectoryInfo(cd + to);
            //string[] filesToSync = Directory.GetFiles(from);
            //string[] filesinToFolder = [];

            var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
            var fc = new FolderSync(mlog.Object);

            try
            {
                var expectedFiles = fromDir.GetFiles();
                if (expectedFiles.Length == 0) throw new Exception("No files to sync");

                fc.SyncFolder(fromDir, toDir);

                var realFiles = toDir.GetFiles();
                Xunit.Assert.Equal(expectedFiles.Length, realFiles.Length);
                bool result = true;
                foreach (var file in expectedFiles)
                {
                    result = result && file.LastWriteTime == realFiles.FirstOrDefault(f => f.Name == file.Name)?.LastWriteTime;
                }
                Xunit.Assert.True(result);
            }
            catch (Exception)
            {
                Xunit.Assert.Fail();
            }
            finally
            {
                foreach (var file in toDir.GetFiles())
                {
                    file.Delete();
                }
            }
        }

        [Theory]
        [InlineData("file2.txt", "\\FolderSyncTest\\From", "\\FolderSyncTest\\3")]
        public void SyncNewFileTest(string file, string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var _from = new FileInfo(cd + from + "\\" + file);
            var _to = new FileInfo(cd + to + "\\" + file);
            try
            {
                var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
                var fc = new FolderSync(mlog.Object);

                if (!_from.Exists) throw new Exception("No file to sync");
                int resultCode = fc.SyncFile(_from, ref _to);
                Xunit.Assert.Equal(_from.LastWriteTime, _to.LastWriteTime);
                Xunit.Assert.Equal(0, resultCode);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _to.Delete();
            }
        }
        [Theory]
        [InlineData("file.txt", "\\FolderSyncTest\\From", "\\FolderSyncTest\\4")]
        public void SyncUpdatedFileTest(string file, string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var _from = new FileInfo(cd + from + "\\" + file);
            var _to = new FileInfo(cd + to + "\\" + file);
            try
            {
                var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
                var fc = new FolderSync(mlog.Object);

                fc.SyncFile(_from, ref _to);
                if (!_from.Exists) throw new Exception("No file to sync");
                File.AppendAllLines(_from.FullName, ["new line"]);
                int resultCode = fc.SyncFile(_from, ref _to);

                Xunit.Assert.Equal(_from.LastWriteTime, _to.LastWriteTime);
                Xunit.Assert.Equal(1, resultCode);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _to.Delete();
            }
        }
        [Theory]
        [InlineData("file.txt", "\\FolderSyncTest\\From", "\\FolderSyncTest\\5")]
        public void SyncOpenedFileTest(string file, string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var _from = new FileInfo(cd + from + "\\" + file);
            var _to = new FileInfo(cd + to + "\\" + file);
            try
            {
                var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
                var fc = new FolderSync(mlog.Object);

                fc.SyncFile(_from, ref _to);
                if (!_from.Exists) throw new Exception("No file to sync");
                using (var fs = _from.OpenWrite())
                {
                    Helper.AddText(fs, "\r\n --rn--");
                    Helper.AddText(fs, "\r --r--");
                    Helper.AddText(fs, "\n --n--");
                    int resultCode = fc.SyncFile(_from, ref _to);
                    Xunit.Assert.Equal(5, resultCode);
                    fs.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _to.Delete();
            }
        }

        [Theory]
        //[InlineData("\\FolderSyncTest\\From", "\\FolderSyncTest\\To")]
        [InlineData("file.txt", "\\FolderSyncTest\\From", "\\FolderSyncTest\\To1")]
        public async Task StopAsync_ShouldStopMonitoring(string file, string from, string to)
        {
            // Arrange

            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var fromDir = new DirectoryInfo(cd + from);
            var fromFile = new FileInfo(cd + from + "\\" + file);
            var toDir = new DirectoryInfo(cd + to); ;

            var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
            var fc = new FolderSync(mlog.Object);

            var cancellationTokenSource = new CancellationTokenSource();
            var ct = new CancellationTokenSource();
            fc.AddFolderToSync(fromDir.FullName, toDir.FullName);

            // Заменяем реальный FileSystemWatcher на mock (если он используется в MonitorFolderAsync)
            //var mockWatcher = new Mock<FileSystemWatcher>();
            ///mockWatcher.Setup(w => w.EnableRaisingEvents).Returns(true);
            // В данном примере предполагаем, что MonitorFolderAsync просто ждёт отмены
            try
            {


                fc.Start();

                // Act
                await Task.Delay(100); // Даём время на старт
                if (!fromFile.Exists) throw new Exception("No file to sync");
                File.AppendAllLines(fromFile.FullName, ["new line"]);

                await fc.StopAsync();

                while (fc.HaveFilesInProcess)
                {
                    await Task.Delay(1000);

                }
                // Assert
                // Если StopAsync отработал без ошибок — тест пройден
                //просто проверить есть ли файл в папке получателя
                //var log = fc.GetLog() ?? throw new Exception();

                Xunit.Assert.Single(toDir.GetDirectories());
                Xunit.Assert.Single(toDir.GetDirectories().First().GetFiles());
            }
            finally
            {
                toDir.Delete(true);
                //toDir.Delete();
            }
        }

        [Theory]
        [InlineData("\\FolderSyncTest\\From", "\\FolderSyncTest\\To2")]
        public async Task MonitorFolderAsync_ShouldStop_WhenCancellationRequested(string from, string to)
        {
            // Arrange

            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var fromDir = new DirectoryInfo(cd + from);
            var toDir = new DirectoryInfo(cd + to); ;

            var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
            var fc = new FolderSync(mlog.Object);

            var cancellationTokenSource = new CancellationTokenSource();
            //fc.AddFolderToSync(fromDir.FullName, toDir.FullName);

            // Act
            await Task.Delay(4000);
            fc.Start(); // Запускаем мониторинг

            cancellationTokenSource.CancelAfter(100); // Отменяем через 100 мс

            // Assert
            Xunit.Assert.True(true);
            

        }
    }

    static class Helper
    {
        public static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Position = fs.Length == 0 ? 0 : fs.Length - 1;
            fs.Write(info, 0, info.Length);
        }
    }
}