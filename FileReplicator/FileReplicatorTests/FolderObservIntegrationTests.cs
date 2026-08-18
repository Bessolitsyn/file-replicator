using Castle.Core.Logging;
using FileReplicator;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.Utilities;
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
    #region Observing Fixture

    public class ObservingFixture : IDisposable
    {
        public DirectoryInfo FromDir { get; private set; }
        public DirectoryInfo ToDir { get; private set; }
        public DirectoryInfo SubDir { get; private set; }
        public DirectoryInfo SubDir2 { get; private set; }
        public FileInfo[] InitialFiles { get; private set; }
        public FolderSync? FolderSync { get; private set; }
        public bool IsStarted { get; set; }

        public ObservingFixture()
        {
            InitializeAsync().Wait();
        }

        private async Task InitializeAsync()
        {
            // Arrange — общий код из оригинального теста ObservingTest
            FromDir = new DirectoryInfo("from3");
            SubDir = FromDir.CreateSubdirectory("sub");
            SubDir2 = FromDir.CreateSubdirectory("sub2");
            ToDir = new DirectoryInfo("to3");
            FromDir.Create();
            ToDir.Create();
            InitialFiles = Helper.CreaeteFilesAndAddText(FromDir, 5);
            IsStarted = false;
            _ = Helper.CreaeteFilesAndAddText(SubDir2, 2);

        }

        public void StartObserve(ITestOutputHelper output)
        {
            var mlog = new Helper.XUnitLogger(output, "TestLogger");
            var folderSync = new FolderSync(mlog);
            folderSync.SetFolderToObserve(FromDir, ToDir);
            folderSync.StartObserving();
            Task.Delay(100).Wait(); // Даём время на старт
            FolderSync = folderSync;
        }

        public void StopObserve()
        {
            _ = FolderSync?.StopObservingAsync().Result;
            FolderSync = null;
        }

        public void Dispose()
        {
            //if (IsStarted) FolderSync.StopObservingAsync().Wait();
            ToDir?.Delete(true);
            FromDir?.Delete(true);
        }
    }

    [CollectionDefinition("ObservingCollection")]
    public class ObservingCollection : ICollectionFixture<ObservingFixture>
    {
        // Этот класс не содержит кода — он просто определяет коллекцию
    }

    #endregion

    [Collection("ObservingCollection")]
    public class ObservingFileCreationAndModification : TestClassWithOutpu, IDisposable
    {
        private readonly ObservingFixture _fixture;
        private readonly DirectoryInfo _fromDir;
        private readonly DirectoryInfo _toDir;
        private readonly DirectoryInfo _subDir;
        private readonly FileInfo[] _files;

        public ObservingFileCreationAndModification(ObservingFixture fixture, ITestOutputHelper output) : base(output)
        {
            _fixture = fixture;
            _fromDir = fixture.FromDir;
            _toDir = fixture.ToDir;
            _subDir = fixture.SubDir;
            _files = fixture.InitialFiles;
        }

        [Fact]
        public async Task Observing_FileCreationAndModification()
        {
            _fixture.StartObserve(Output);

            // Act — Создание новых файлов
            var newfiles = Helper.CreaeteFilesAndAddText(_fromDir, 5, "txt2");
            // Изменение существующего
            _ = Helper.AddText(_files[2]);
            await Task.Delay(200);

            // Assert
            Xunit.Assert.Equal(10, _fromDir.GetFiles().Length);
            Xunit.Assert.Equal(6, _toDir.GetFiles().Length);

            _fixture.StopObserve();
        }

        [Fact]
        public async Task Observing_FileCreationInSubdirectory()
        {
            _fixture.StartObserve(Output);
            // Act — Создание новых файлов в поддиректории
            _ = Helper.CreaeteFilesAndAddText(_subDir, 2);
            await Task.Delay(200);

            // Assert
            Xunit.Assert.Equal(2, _toDir
                .GetDirectories()
                    .First(d => d.Name == _subDir.Name)
                    .GetFiles().Length
                );

            _fixture.StopObserve();

        }

        [Fact]
        public async Task Observing_FileRenaming()
        {
            _fixture.StartObserve(Output);
            // Arrange
            var newfiles = Helper.CreaeteFilesAndAddText(_fromDir, 5, "txt2");

            // Act — Переименование
            _files[3].MoveTo(Path.Combine(_files[3].Directory.FullName, "newnamedFile.txt"));
            var oldName = newfiles[3].Name;
            newfiles[3].MoveTo(Path.Combine(newfiles[3].Directory.FullName, "newnamedFile.txt2"));
            await Task.Delay(200);

            // Assert
            Xunit.Assert.Contains(_toDir.GetFiles(), f => f.Name == "newnamedFile.txt");
            Xunit.Assert.Contains(_toDir.GetFiles(), f => f.Name == "newnamedFile.txt2");
            Xunit.Assert.DoesNotContain(_toDir.GetFiles(), f => f.Name == oldName);

            _fixture.StopObserve();

        }

        [Fact]
        public async Task Observing_FileMovingAndDeletion()
        {
            _fixture.StartObserve(Output);

            // Arrange
            var newfiles = Helper.CreaeteFilesAndAddText(_fromDir, 5, "txt2");

            // Act — Перемещение файла
            newfiles[4].MoveTo(Path.Combine(_subDir.FullName, newfiles[4].Name));
            await Task.Delay(200);

            // Assert
            Xunit.Assert.DoesNotContain(_toDir.GetFiles(), f => f.Name == newfiles[4].Name);
            Xunit.Assert.Contains(_toDir.GetDirectories(_subDir.Name).First().GetFiles(), f => f.Name == newfiles[4].Name);

            // Act — Удаление файла
            newfiles[0].Delete();
            await Task.Delay(200);

            // Assert
            Xunit.Assert.DoesNotContain(_toDir.GetFiles(), f => f.Name == newfiles[0].Name);

            _fixture.StopObserve();

        }

        public void Dispose()
        {
            //// Очистка после теста
            //foreach (var f in _fromDir.GetFiles()) f.Delete();
            //foreach (var d in _fromDir.GetDirectories()) d.Delete(true);
            //foreach (var f in _toDir.GetFiles()) f.Delete();
            //foreach (var d in _toDir.GetDirectories()) d.Delete(true);

            //// Восстанавливаем начальные файлы
            //_ = Helper.CreaeteFilesAndAddText(_fromDir, 5);
            //_ = Helper.CreaeteFilesAndAddText(_fixture.SubDir2, 2);
        }
    }

    [Collection("ObservingCollection")]
    public class ObservingDirectoryOperations : TestClassWithOutpu, IDisposable
    {
        private readonly ObservingFixture _fixture;
        private readonly DirectoryInfo _fromDir;
        private readonly DirectoryInfo _toDir;
        private readonly DirectoryInfo _subDir;
        private readonly DirectoryInfo _subDir2;
        private readonly FileInfo[] _files;

        public ObservingDirectoryOperations(ObservingFixture fixture, ITestOutputHelper output) : base(output)
        {
            _fixture = fixture;
            _fromDir = fixture.FromDir;
            _toDir = fixture.ToDir;
            _subDir = fixture.SubDir;
            _subDir2 = fixture.SubDir2;
            _files = fixture.InitialFiles;
        }

        [Fact]
        public async Task Observing_DirectoryCreation()
        {
            _fixture.StartObserve(Output);

            // Act — Создание поддиректории с файлами
            var sub = _fromDir.CreateSubdirectory("teb");
            var fls = Helper.CreaeteFilesAndAddText(sub, 2);
            await Task.Delay(200);

            // Assert
            Xunit.Assert.Equal(2, _fromDir.GetDirectories("teb").First().GetFiles().Length);
            Xunit.Assert.Equal(2, _toDir.GetDirectories("teb").First().GetFiles().Length);

            _fixture.StopObserve();

        }

        [Fact]
        public async Task Observing_DirectoryRenaming_ExistingTarget()
        {
            _fixture.StartObserve(Output);

            // Arrange
            var sub = _fromDir.CreateSubdirectory("teb");
            var fls = Helper.CreaeteFilesAndAddText(sub, 2);
            await Task.Delay(200);

            // Act — Переименование директории с файлами - копия директории существует в TO
            sub.MoveTo(Path.Join(sub.Parent.FullName, "teb2"));
            await Task.Delay(200);

            // Assert
            Xunit.Assert.Equal(2, _toDir.GetDirectories("teb2").First().GetFiles().Length);
            Xunit.Assert.DoesNotContain(_toDir.GetDirectories(), f => f.Name == "teb");

            _fixture.StopObserve();

        }

        [Fact]
        public async Task Observing_DirectoryRenaming_NonExistingTarget()
        {
            _fixture.StartObserve(Output);

            // Act — Переименование директории с файлами - копия директории не существует в TO
            _subDir2.MoveTo(Path.Join(_subDir2.Parent.FullName, "subdir_moved"));
            await Task.Delay(200);
            // ФОНОВОЕ КОПИРОВАНИЕ В ТАСКЕ его надо ждать

            // Assert
            Xunit.Assert.Equal(2, _toDir.GetDirectories("subdir_moved").First().GetFiles().Length);

            _fixture.StopObserve();
        }

        public void Dispose()
        {
            //// Очистка после теста
            //foreach (var f in _fromDir.GetFiles()) f.Delete();
            //foreach (var d in _fromDir.GetDirectories()) d.Delete(true);
            //foreach (var f in _toDir.GetFiles()) f.Delete();
            //foreach (var d in _toDir.GetDirectories()) d.Delete(true);

            //// Восстанавливаем начальные файлы и структуру
            //_ = Helper.CreaeteFilesAndAddText(_fromDir, 5);
            //_ = Helper.CreaeteFilesAndAddText(_fromDir.CreateSubdirectory("sub"), 2);
            //_ = Helper.CreaeteFilesAndAddText(_fromDir.CreateSubdirectory("sub2"), 2);
        }
    }

    [Collection("ObservingCollection")]
    public class ObservingStartStopTest(ITestOutputHelper output) : TestClassWithOutpu(output)
    {
        [Fact]
        public void Observing_Start_Stop()
        {

            // Arrange
            var fromDir = new DirectoryInfo("from_start");
            var toDir = new DirectoryInfo("to_start");
            fromDir.Create();
            toDir.Create();
            var mlog = new Helper.XUnitLogger(Output, "ObservingStartTest");
            var fc = new FolderSync(mlog);

            try
            {
                // Act
                fc.SetFolderToObserve(fromDir, toDir);
                bool isStarted = fc.StartObserving();

                // Assert
                Xunit.Assert.True(isStarted);
                Xunit.Assert.True(fc.IsObserving);

                fc.StopObservingAsync().Wait();

                // Assert
                Xunit.Assert.True(!fc.IsObserving);

            }
            finally
            {
                fromDir.Delete(true);
                toDir.Delete(true);
            }
        }
    }

    public class TestClassWithOutpu(ITestOutputHelper output)
    {
        protected readonly ITestOutputHelper Output = output;

    }
}
