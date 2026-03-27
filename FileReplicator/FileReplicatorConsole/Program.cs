using FileReplicator;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        //if (args.Length == 0)
        //{
        //    Console.WriteLine("Hello, It's  The Reptor - Folder To Folder replicator (folder synchronizer)  tool");
        //    Console.WriteLine("Usage: ReptorConsole [settings.csv] [-start]");
        //    return;
        //}

        string settingsPath = "settings.csv";
        bool doInitialSync = false;
        if (args.Length == 1)
        {
            doInitialSync = args[0].Equals("-start", StringComparison.OrdinalIgnoreCase);
            if (!doInitialSync) settingsPath = args[0];
        }
        if (args.Length == 2)
        {
            doInitialSync = args[1].Equals("-start", StringComparison.OrdinalIgnoreCase);
            if (!doInitialSync)
            {
                Console.WriteLine("Usage: ReptorConsole [settings.csv] [-start]");
                return;
            }
            settingsPath = args[0];
        }

        var file = new FileInfo(settingsPath);

        //if (!File.Exists(settingsPath))
        if (!file.Exists)
        {
            Console.WriteLine($"Settings file not found: {settingsPath}");
            return;
        }

        //string file = File.ReadAllText(settingsPath);
        //var settings = Settings.GetSettingsFromJSON(json);
        var settings = Settings.GetSettingsFromCSV(file);

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("Reptor_Console");
        using var replicator = new Replicator(settings, logger);

        if (doInitialSync)
        {
            Console.WriteLine("Running initial sync...");
            await replicator.ExecuteAsync();
        }
        else { 
            Console.WriteLine("Monitoring folders...");
            replicator.Start();

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();

            await replicator.StopAsync();
        }
    }
}
