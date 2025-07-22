using FileReplicator;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace FileReplicator.Tests.Integration
{
    public class ReplicatorTests
    {
        [Theory]
        [InlineData(@"\FolderSyncTest\From", @"\FolderSyncTest\To")]
        public void ExecuteReplicatorWithSettingsTest(string from, string to)
        {
            
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            string escapedFrom = JsonSerializer.Serialize(cd + from); 
            string escapedTo = JsonSerializer.Serialize(cd + to);
            string json = "{\"SourceFolders\":[{\"OriginalPath\":" + escapedFrom + ",\"DestinationPath\":" + escapedTo + "}]}";
            var s = Settings.GetSettingsFromJSON(json);


            var mlog = new Mock<Microsoft.Extensions.Logging.ILogger>();
            var fc = new FolderSync(mlog.Object);

            using (Replicator rep = new(s, mlog.Object))
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