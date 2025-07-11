using Xunit;
using FileReplicator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Json;

namespace FileReplicator.Tests
{
    public class ReplicatorTests
    {
        [Theory]
        [InlineData(@"\FolderSyncTest\From", @"\FolderSyncTest\To")]
        public void ExecuteReplicatorTest(string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            string escapedFrom = JsonSerializer.Serialize(cd + from); 
            string escapedTo = JsonSerializer.Serialize(cd + to);
            string json = "{\"SourceFolders\":[{\"OriginalPath\":" + escapedFrom + ",\"DestinationPath\":" + escapedTo + "}]}";
            var s = Settings.GetSettingsFromJSON(json);

            using (Replicator rep = new(s))
            {   
                Xunit.Assert.Equal(0, rep.Status);
                rep.Execute();
            }

        }
    }
}