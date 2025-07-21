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
        [Theory]
        [InlineData("s", "d")]
        [InlineData("{\"PropA\": \"a\"}", "d")]
        public void GetSettingsFromJSONTest(string from, string to)
        {
            string json = "{\"SourceFolders\":[{\"OriginalPath\":\"" + from + "\",\"DestinationPath\":\"" + to + "\"}]}";
            
            var settings = new Settings();
            try
            {
                Settings s = Settings.GetSettingsFromJSON(json);
                Xunit.Assert.NotNull(s);
                Xunit.Assert.NotNull(s.SourceFolders);
                Xunit.Assert.True(s.SourceFolders.Length > 0);

            }
            catch (Exception ex)
            {
                Xunit.Assert.True(ex.Message == ServiceMessages.ExceptionMessageCode.NoSettings.ToString());
            }
        }
            
    }
}