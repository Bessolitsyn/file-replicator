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
        public SourceFolder[] SourceFolders { get; set; } = [];
        [Obsolete]
        public static Settings GetSettingsFromJSON(string json)
        {
            //var s = new Settings();
            Settings settings;
            try
            {
                settings = JsonSerializer.Deserialize<Settings>(json) ?? throw new Exception(ServiceMessages.ExceptionMessageCode.NoSettings.ToString());
                if (!settings.IsValid(out var error))
                {
                    Console.WriteLine($"Settings validation failed: {error}");
                    // Handle error
                }
                return settings;
            }
            catch (Exception ex)
            {
                var message=ServiceMessages.ExceptionMessageCode.NoSettings.ToString();
                throw new Exception(message, ex);
            }
        }
        public static Settings GetSettingsFromCSV(FileInfo file)
        {
            var settings = new Settings();
            try
            {
                            
                List<string[]> lines=[];
                if (file.Exists)
                {
                    using var strmR = new StreamReader(file.OpenRead(), Encoding.UTF8);
                    while (!strmR.EndOfStream)
                    {
                        var line = (strmR.ReadLine())?.Split(";");
                        if (line !=null) lines.Add(line);
                    }
                    strmR.Close();
                }
                settings.SourceFolders = new SourceFolder[lines.Count-1];
                for (int i = 0; i < lines.Count-1; i++)
                {
                    settings.SourceFolders[i] = new SourceFolder(lines[i + 1][0], lines[i + 1][1]);
                }
            }
            catch (Exception ex)
            {
                var message = ServiceMessages.ExceptionMessageCode.NoSettings.ToString();
                throw new Exception(message, ex);
            }
            return settings;
        }

        public bool IsValid(out string error)
        {
            error = string.Empty;

            // Perform your validation here. For example:
            if (SourceFolders == null || SourceFolders.Length == 0)
            {
                error = "No source folders configured.";
                return false;
            }

            foreach (var folder in SourceFolders)
            {
                if (string.IsNullOrWhiteSpace(folder.OriginalPath) || string.IsNullOrWhiteSpace(folder.DestinationPath))
                {
                    error = "OriginalPath and DestinationPath must be provided for all source folders.";
                    return false;
                }
            }
                
            return true;
        }
    }
    public record SourceFolder(
        string OriginalPath,
        string DestinationPath
    );


}

