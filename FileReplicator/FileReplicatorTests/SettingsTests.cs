using Xunit;
using FileReplicator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace FileReplicator.Tests.Unit
{
    public class SettingsTests
    {

        [Fact]
        public void GetSettingsFromRealCSVFileTest()
        {
            FileInfo csv = new FileInfo("..\\..\\..\\settings.csv");

            //var settings = new Settings();
            //settings.SourceFolders = [new SourceFolder(from.FullName, to.FullName)];
            //var bytes = JsonSerializer.SerializeToUtf8Bytes<Settings>(settings);
            //Helper.AddBytes(json, bytes);           

            Settings s = Settings.GetSettingsFromCSV(csv);
            Xunit.Assert.NotNull(s);
            Xunit.Assert.NotNull(s.SourceFolders);
            Xunit.Assert.True(s.SourceFolders.Length > 0);


        }

    }
}