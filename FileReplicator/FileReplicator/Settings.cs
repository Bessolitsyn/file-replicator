using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileReplicator
{
    /// <summary>
    /// Represents the configuration settings for the file replication service.
    /// <para>Представляет настройки конфигурации для службы репликации файлов.</para>
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// A list of source and destination folders to be synchronized.
        /// <para>Список исходных и целевых папок для синхронизации.</para>
        /// </summary>
        public SourceFolder[] SourceFolders { get; set; } = [];

        /// <summary>
        /// Obsolete: Deserializes settings from a JSON string.
        /// <para>Устарело: Десериализует настройки из строки JSON.</para>
        /// </summary>
        /// <param name="json">The JSON string containing settings. / JSON-строка, содержащая настройки.</param>
        /// <returns>A <see cref="Settings"/> object. / Объект <see cref="Settings"/>.</returns>
        /// <exception cref="Exception">Thrown if deserialization fails or required properties are missing. / Выбрасывается, если десериализация не удалась или отсутствуют обязательные свойства.</exception>
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

        /// <summary>
        /// Loads settings from a CSV file.
        /// <para>Загружает настройки из CSV-файла.</para>
        /// </summary>
        /// <param name="file">The CSV file containing settings. / CSV-файл, содержащий настройки.</param>
        /// <returns>A <see cref="Settings"/> object. / Объект <see cref="Settings"/>.</returns>
        /// <exception cref="Exception">Thrown if reading fails. / Выбрасывается, если чтение не удалось.</exception>
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

        /// <summary>
        /// Validates the current settings object to ensure all required fields are present.
        /// <para>Проверяет текущий объект настроек, чтобы убедиться, что все обязательные поля заполнены.</para>
        /// </summary>
        /// <param name="error">Output parameter that contains the error message if validation fails. / Выходной параметр, содержащий сообщение об ошибке, если проверка не удалась.</param>
        /// <returns>True if settings are valid; otherwise, false. / True, если настройки верны; иначе false.</returns>
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

    /// <summary>
    /// A record containing the path to the original folder and its corresponding destination path.
    /// <para>Запись, содержащая путь к исходной папке и соответствующий ей путь назначения.</para>
    /// </summary>
    /// <param name="OriginalPath">The path to the source directory. / Путь к исходному каталогу.</param>
    /// <param name="DestinationPath">The path to the target directory. / Путь к целевому каталогу.</param>
    public record SourceFolder(
        string OriginalPath,
        string DestinationPath
    );


}
