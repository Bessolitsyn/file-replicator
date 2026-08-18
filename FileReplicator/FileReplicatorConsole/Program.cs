using FileReplicator;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Folder To Folder replicator (folder synchronizer)");

        var settingsArg = new Argument<string>(
            name: "settings",
            description: "Path to settings CSV file (default: settings.csv in current directory)",
            getDefaultValue: () => "settings.csv"
        );

        var startOption = new Option<bool>(
            aliases: ["--start", "-s"],
            description: "Run initial sync and exit. If not specified, runs in folder monitoring mode (watches for changes in source folder)"
        );

        var coresOption = new Option<int>(
            aliases: ["--cores", "-c"],
            description: "Number of CPU cores to use, (default: All cores of CPU)",
            getDefaultValue: () => 0
        );

        rootCommand.AddArgument(settingsArg);
        rootCommand.AddOption(startOption);
        rootCommand.AddOption(coresOption);

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            string settingsPath = context.ParseResult.GetValueForArgument(settingsArg);
            bool doInitialSync = context.ParseResult.GetValueForOption(startOption);
            int cores = context.ParseResult.GetValueForOption(coresOption);

            var file = new FileInfo(settingsPath);

            if (!file.Exists)
            {
                Console.WriteLine($"Settings file not found: {settingsPath}");
                context.ExitCode = 1;
                return;
            }

            var settings = Settings.GetSettingsFromCSV(file);

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger("Reptor_Console");
            using var replicator = new Replicator(settings, logger);

            if (doInitialSync)
            {
                Console.WriteLine("Running initial sync...");
                await replicator.ExecuteAsync(cores);
            }
            else
            {
                Console.WriteLine("Monitoring folders...");
                replicator.Start(cores);

                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();

                await replicator.StopAsync();
            }
        });

        return await rootCommand.InvokeAsync(args);
    }
}
