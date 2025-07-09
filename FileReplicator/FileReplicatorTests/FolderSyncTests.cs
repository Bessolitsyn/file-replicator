using FileReplicator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace FileReplicatorTests
{
    public class FolderSyncTests
    {
        [Theory]
        [InlineData("sdadds", "sdsdds")]
        [InlineData("\\FolderSyncTest\\From\\123", "\\FolderSyncTest\\To")]
        public void AddFolderToSyncTest_WithNotValidArguments(string from, string to)
        {
            try
            {
                var fc = new FolderSync();
                fc.AddFolderToSync(from, to);
                fc.Sync();
                Xunit.Assert.Single(fc.GetFolderToSync());
            }
            catch (Exception)
            {
                Xunit.Assert.Fail();
            }
            finally
            {
                Directory.Delete(from, true);
                Directory.Delete(to, true);
            }


        }
        [Theory]
        [InlineData("\\FolderSyncTest\\From", "\\FolderSyncTest\\To")]
        public void SyncFilesTest(string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var fc = new FolderSync();
            var files = new Queue<(FileInfo from, FileInfo to)>();
            foreach (var f in Directory.GetFiles(cd + from))
            {
                files.Enqueue((from: new FileInfo(f), to:new FileInfo(f.Replace(from, to))));
            }

            try
            {
                using (var fs = File.OpenWrite(cd + from + "\\file.txt"))
                {
                    Helper.AddText(fs, "\r\n --rn--");
                    Helper.AddText(fs, "\r --r--");
                    Helper.AddText(fs, "\n --n--");
                    var lockedFiles = fc.SyncFiles(files);
                    fs.Close();
                }
                Xunit.Assert.Equal(Directory.GetFiles(cd + from).Length, Directory.GetFiles(cd + to).Length + 1);

            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Directory.GetFiles(cd + to).ToList().ForEach(f => File.Delete(f));
            }

        }

        [Theory]
        [InlineData("\\FolderSyncTest\\From", "\\FolderSyncTest\\To")]
        public void SyncFolderTest(string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            var fromDir = new DirectoryInfo(cd + from);
            var toDir = new DirectoryInfo(cd + to);
            //string[] filesToSync = Directory.GetFiles(from);
            //string[] filesinToFolder = [];
            var fc = new FolderSync();
            try
            {
                var expectedFiles = fromDir.GetFiles();
                if (expectedFiles.Length == 0) throw new Exception("No files to sync");

                fc.SyncFolder(fromDir, toDir);

                var realFiles = toDir.GetFiles();
                Xunit.Assert.Equal(expectedFiles.Length, realFiles.Length);
                bool result = true;
                foreach (var file in expectedFiles)
                {
                    result = result && file.LastWriteTime == realFiles.FirstOrDefault(f => f.Name == file.Name)?.LastWriteTime;
                }
                Xunit.Assert.True(result);
            }
            catch (Exception)
            {
                Xunit.Assert.Fail();
            }
            finally
            {
                foreach (var file in toDir.GetFiles())
                {
                    file.Delete();
                }
            }
        }

        [Theory]
        [InlineData("file.txt", "\\FolderSyncTest\\From\\", "\\FolderSyncTest\\To\\")]
        public void SyncNewFileTest(string file, string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            from = cd + from + file;
            to = cd + to + file;
            var fc = new FolderSync();
            if (!File.Exists(from)) throw new Exception("No file to sync");
            int resultCode = fc.SyncFile(from, to);
            Xunit.Assert.Equal(File.GetLastWriteTime(from), File.GetLastWriteTime(to));
            Xunit.Assert.Equal(0, resultCode);
            File.Delete(to);
        }
        [Theory]
        [InlineData("file.txt", "\\FolderSyncTest\\From\\", "\\FolderSyncTest\\To\\")]
        public void SyncUpdatedFileTest(string file, string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            from = cd + from + file;
            to = cd + to + file;
            var fc = new FolderSync();
            fc.SyncFile(from, to);
            if (!File.Exists(from)) throw new Exception("No file to sync");
            File.AppendAllLines(from, ["new line"]);
            int resultCode = fc.SyncFile(from, to);
            Xunit.Assert.Equal(File.GetLastWriteTime(from), File.GetLastWriteTime(to));
            Xunit.Assert.Equal(1, resultCode);
            File.Delete(to);
        }
        [Theory]
        [InlineData("file.txt", "\\FolderSyncTest\\From\\", "\\FolderSyncTest\\To\\")]
        public void SyncOpenedFileTest(string file, string from, string to)
        {
            var cd = Environment.CurrentDirectory + "\\..\\..\\..";
            from = cd + from + file;
            to = cd + to + file;
            var fc = new FolderSync();
            fc.SyncFile(from, to);
            if (!File.Exists(from)) throw new Exception("No file to sync");
            using (var fs = File.OpenWrite(from))
            {
                Helper.AddText(fs, "\r\n --rn--");
                Helper.AddText(fs, "\r --r--");
                Helper.AddText(fs, "\n --n--");
                int resultCode =  fc.SyncFile(from, to);
                Xunit.Assert.Equal(5, resultCode);
                fs.Close();
            }
            File.Delete(to);
        }

    }

    static class Helper
    {
        public static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Position = fs.Length - 1;
            fs.Write(info, 0, info.Length);
        }
    }
}