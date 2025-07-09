using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileReplicator
{
    public class Settings
    {
        public SourceFolder[]? SourceFolders { get; set; }

        public static Settings GetSettingsFromJSON(string json)
        {
            var s = new Settings();
            try
            {
                s = JsonSerializer.Deserialize<Settings>(json);
            }
            catch (Exception ex)
            {
                var message=ServiceMessages.ExceptionMessageCode.NoSettings.ToString();
                throw new Exception(message, ex);
            }
            return s ?? throw new Exception(ServiceMessages.ExceptionMessageCode.NoSettings.ToString());
        }
            
    }
    public record SourceFolder(
        string OriginalPath,
        string DestinationPath
    );


}
