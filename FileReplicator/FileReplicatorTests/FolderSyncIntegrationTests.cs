using Castle.Core.Logging;
using FileReplicator;
using Microsoft.Extensions.Logging;
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
using Xunit.Abstractions;
using Xunit.Sdk;
using static FileReplicator.ServiceMessages;
using static System.Net.WebRequestMethods;


namespace FileReplicator.Tests.Integration
{
    public class FolderSyncTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;
        FolderSync GetFolderSync(DirectoryInfo from, DirectoryInfo to)
        {
            //var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
            var mlog = new Helper.XUnitLogger(_output, "TestLogger");
            var fc = new FolderSync(mlog);
            fc.SerFolderToSync(from, to);
            return fc;
        }
        [Fact]
        public async Task BasicReplicate()
        {
            var fromDir = new DirectoryInfo("from");
            var toDir = new DirectoryInfo("to");
            try
            {
                var mlog = new Helper.XUnitLogger(_output, "TestLogger");
                var fc = new FolderSync(mlog);

                fc.SerFolderToSync(fromDir, toDir);
                await fc.ReplicateAsync();
            }
            catch (Exception)
            {
                Xunit.Assert.Fail();
            }
            finally
            {
                fromDir.Delete(true);
                toDir.Delete(true);
            }

        }

        [Fact]
        public async Task RepitedReplicateAfterFileAddingAndFileEditing()
        {
            //ARRANGE
            var fromDir = new DirectoryInfo("from1");
            fromDir.Create();
            var toDir = new DirectoryInfo("to1");
            toDir.Create();
            var files = Helper.CreaeteFilesAndAddText(fromDir, 5);
            if (fromDir.GetFiles().Length == 0) throw new Exception("No files to sync");

            try
            {
                var fc = GetFolderSync(fromDir, toDir);
                await fc.ReplicateAsync();
                Xunit.Assert.Equal(fromDir.GetFiles().Length, files.Length);
                Xunit.Assert.Equal(toDir.GetFiles().Length, files.Length);//на один файл больше так как там еще появляется файл журнала

                //ACT
                await Task.Delay(500);
                var newFile = Helper.CreaeteFileAndAddText(fromDir, "newfile.txt");
                var editedFile = files[3];
                Helper.AddText(editedFile, "New Text Line");

                await fc.ReplicateAsync();
                editedFile.Refresh();

                Xunit.Assert.Equal(fromDir.GetFiles().Length, files.Length + 1);
                Xunit.Assert.Equal(toDir.GetFiles().Length, files.Length + 1);
                Xunit.Assert.Contains(newFile.Name, toDir.GetFiles().Select(f => f.Name));
                Xunit.Assert.Equal(editedFile.Length, toDir.GetFiles().First(f => f.Name == editedFile.Name).Length);
            }
            finally
            {
                fromDir.Delete(true);
                toDir.Delete(true);
            }
        }
        [Fact]
        public async Task RepitedReplicateAfterFileAndSubdirDeleting()
        {
            //ARRANGE
            var fromDir = new DirectoryInfo("from19");
            fromDir.Create();
            var subdir = fromDir.CreateSubdirectory("subdir");
            var subdirDel = subdir.CreateSubdirectory("to_delete");
            var subdir2 = subdir.CreateSubdirectory("subdir2");
            var toDir = new DirectoryInfo("to19");
            toDir.Create();
            var filesInFromDir = Helper.CreaeteFilesAndAddText(fromDir, 5);
            var filesInSubdir = Helper.CreaeteFilesAndAddText(subdir, 5);
            _ = Helper.CreaeteFilesAndAddText(subdirDel, 5);
            var filesInSubdir2 = Helper.CreaeteFilesAndAddText(subdir2, 5);
            if (fromDir.GetFiles().Length == 0) throw new Exception("No files to sync");
            if (subdir.GetFiles().Length == 0) throw new Exception("No files to sync");

            try
            {
                var fc = GetFolderSync(fromDir, toDir);
                await fc.ReplicateAsync();
                Xunit.Assert.Equal(fromDir.GetFiles().Length, filesInFromDir.Length);
                Xunit.Assert.Equal(toDir.GetFiles().Length, filesInFromDir.Length);
                Xunit.Assert.Equal(toDir.GetDirectories().Single(d => d.Name == subdir.Name).GetFiles().Length, filesInFromDir.Length);

                //ACT
                filesInFromDir[1].Delete();
                filesInFromDir[2].Delete();
                filesInSubdir[3].Delete();
                filesInSubdir2[0].Delete();
                subdirDel.Delete(true);
                await fc.ReplicateAsync();


                Xunit.Assert.Equal(3, fromDir.GetFiles().Length);
                Xunit.Assert.Equal(3, toDir.GetFiles().Length);
                Xunit.Assert.True(!toDir.GetDirectories().Any(d => d.Name == subdirDel.Name));
                Xunit.Assert.Equal(4,
                    toDir.GetDirectories()
                    .First(d => d.Name == subdir.Name)
                    .GetFiles().Length
                );
                Xunit.Assert.Equal(4,
                    toDir.GetDirectories()
                    .First(d => d.Name == subdir.Name)
                    .GetDirectories()
                    .First(d => d.Name == subdir2.Name)
                    .GetFiles()
                    .Length
                );
            }
            finally
            {
                fromDir.Delete(true);
                toDir.Delete(true);
            }
        }
        //todo Добавить тесты на переименование вложенных папок и перемещение удаление  удаление ненужных в получателе.

        [Fact]
        public async Task StartStopTest()
        {
            // Arrange
            var fromDir = new DirectoryInfo("from3");
            var toDir = new DirectoryInfo("to3");
            var mlog = new Helper.XUnitLogger(_output, "StartStopTest");
            var fc = new FolderSync(mlog);
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var state1 = fc.IsObserving;
            bool isStarted1 = fc.StartObserving(); // Запускаем мониторинг /не запуститься так как не добавлена папкка
            fc.SetFolderToObserve(fromDir, toDir);
            bool isStarted2 = fc.StartObserving();
            var state2 = fc.IsObserving;
            await Task.Delay(4000);
            await fc.StopObservingAsync(); // Запускаем мониторинг
            var state3 = fc.IsObserving;

            cancellationTokenSource.CancelAfter(100); // Отменяем через 100 мс

            // Assert
            Xunit.Assert.True(!state1);
            Xunit.Assert.True(!isStarted1);
            Xunit.Assert.True(isStarted2);
            Xunit.Assert.True(state2);
            Xunit.Assert.True(!state3);


        }
    }


}