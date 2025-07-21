using FileReplicator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

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
                try
                {
                    rep.Execute();

                }
                catch (Exception)
                {

                    throw;
                }
                finally
                {
                    foreach (var item in s.SourceFolders)
                    { 
                        var di = new DirectoryInfo(item.DestinationPath);
                        di.GetDirectories().ToList().ForEach(d => d.Delete(true));
                        di.GetFiles().ToList().ForEach(d => d.Delete());
                    }
                }
            }
            
        }
    }
}