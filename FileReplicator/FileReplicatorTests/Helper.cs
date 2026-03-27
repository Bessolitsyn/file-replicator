using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace FileReplicator.Tests
{
    public static class Helper
    {
        public static FileInfo CreaeteFileAndAddText(DirectoryInfo dir, string name)
        {
            var file = new FileInfo(Path.Combine(dir.FullName, name));
            var writer = file.CreateText();
            writer.WriteLine("Line");
            writer.Close();
            return file;
        }
        public static FileInfo[] CreaeteFilesAndAddText(DirectoryInfo dir, int count, string ext="txt")
        {
            var res = new FileInfo[count];
            for (int i = 0; i < count; i++)
            {
                res[i] = CreaeteFileAndAddText(dir, $"file_{i}.{ext}");
            }
            return res;
        }
        public static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Position = fs.Length == 0 ? 0 : fs.Length - 1;
            fs.Write(info, 0, info.Length);
        }
        public static void AddBytes(FileInfo file, byte[] info)
        {
            var fs = file.OpenWrite();
            fs.Position = fs.Length == 0 ? 0 : fs.Length - 1;
            fs.Write(info, 0, info.Length);
            fs.Close();
        }

        public static void AddText(FileInfo file, string value)
        {
            var writer = file.CreateText();
            writer.WriteLine(value);
            writer.Close();
        }

        public static string AddText(FileInfo file)
        {
            var str = "Line";
            var writer = file.CreateText();
            writer.WriteLine(str);
            writer.Close();
            return str;
        }

        public class XUnitLogger : ILogger
        {
            private readonly ITestOutputHelper _output;
            private readonly string _categoryName;

            public XUnitLogger(ITestOutputHelper output, string categoryName)
            {
                _output = output;
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) => default!;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                                   Exception exception, Func<TState, Exception, string> formatter)
            {
                _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
            }
        }

    }
}
