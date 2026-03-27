using Castle.Core.Logging;
using FileReplicator;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using static FileReplicator.ServiceMessages;
using static System.Net.WebRequestMethods;


namespace FileReplicator.Tests.Unit
{
    public class FolderSyncTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;
        [Fact]
        public void SyncFileTest()
        {
            //ARRANGE
            var fromDir = new DirectoryInfo("from2");
            fromDir.Create();
            var toDir = new DirectoryInfo("to2");
            toDir.Create();
            var file = Helper.CreaeteFileAndAddText(fromDir, "fileA.txt");
            if (fromDir.GetFiles().Length == 0) throw new Exception("No files to sync");
            var mlog = new Helper.XUnitLogger(_output, "FolderSyncTests");
            var folderSync = new FolderSync(mlog);

            try
            {   
                //ACT
                var destFile = new FileInfo(Path.Combine(toDir.FullName, file.Name));
                var resultCode = folderSync.SyncFile(file, ref destFile);
                //ASSERT
                Xunit.Assert.Equal(file.LastWriteTime, destFile.LastWriteTime);
                Xunit.Assert.Equal(SyncOperationResultsCode.SuccessfulCopying, resultCode);

                //ACT
                resultCode = folderSync.SyncFile(file, ref destFile);
                //ASSERT
                Xunit.Assert.Equal(SyncOperationResultsCode.NoAction, resultCode);

                //ACT
                Helper.AddText(file);
                resultCode = folderSync.SyncFile(file, ref destFile);
                //ASSERT
                Xunit.Assert.Equal(SyncOperationResultsCode.SuccessfulOverwriting, resultCode);

                //ACT
                using var stream = file.AppendText();
                resultCode = folderSync.SyncFile(file, ref destFile);
                stream.Close();
                //ASSERT
                Xunit.Assert.Equal(SyncOperationResultsCode.FileLocked, resultCode);

                //ACT
                file.Delete();
                resultCode = folderSync.SyncFile(file, ref destFile);
                //ASSERT
                Xunit.Assert.Equal(SyncOperationResultsCode.SuccessfulDeleting, resultCode);


            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                toDir.Delete(true);
                fromDir.Delete(true);
            }
        }        

    }

}