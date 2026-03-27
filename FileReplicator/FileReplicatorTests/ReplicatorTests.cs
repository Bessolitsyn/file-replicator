using FileReplicator;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Assert = Xunit.Assert;

namespace FileReplicator.Tests.Integration
{
    public class ReplicatorTest(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;
        
        //TODO развить тесты - вложенные папки - удаление в источнике папки - переименование папки 
        [Fact]
        public async Task ExecuteReplicatorWithSettingsTest()
        {
            // Arrange: создаём временные директории
            var fromDir = new DirectoryInfo("From23");
            fromDir.Create();
            var subDir = fromDir.CreateSubdirectory("sub234");
            var subDir2 = subDir.CreateSubdirectory("subsub234659");
            var toDir = new DirectoryInfo("To64");
            //toDir.Create();
;
            // Формируем JSON для настроек
            string escapedFrom = JsonSerializer.Serialize(fromDir.FullName);
            string escapedTo = JsonSerializer.Serialize(toDir.FullName);
            string json = "{\"SourceFolders\":[{\"OriginalPath\":" + escapedFrom + ",\"DestinationPath\":" + escapedTo + "}]}";
            var sets = Settings.GetSettingsFromJSON(json);
            var file1 = Helper.CreaeteFileAndAddText(fromDir, "fileDertyw.txt");
            var file2 = Helper.CreaeteFileAndAddText(subDir2, "fileAbraham.txt");
            var mlog = new Helper.XUnitLogger(_output, "ReplicatorTest");

            try
            {
                using (Replicator rep = new(sets, mlog))
                {
                    Assert.Equal(0, rep.Status);

                    // Act
                    await rep.ExecuteAsync();

                    Assert.Single(toDir.GetFiles());
                    Assert.Contains(toDir.GetDirectories(), f => f.Name == subDir.Name);
                    Assert.Single(toDir.GetDirectories().First(f=>f.Name==subDir.Name).GetDirectories().First(f => f.Name == subDir2.Name).GetFiles());

                }
            }
            finally
            {
                fromDir.Delete(true);
                toDir.Delete(true);
            }
        }
    }
}