using FileReplicator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FileReplicator.Tests.Integration
{
    public class FileCopyLogTest(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;

        [Fact()]
        public async Task ReadWriteAddFileTest()
        {
;
            //ARRANGE
            var curDir = new DirectoryInfo("Source");
            var dest = new DirectoryInfo("Dest");
            curDir.Create();
            dest.Create();
            try
            {
                var fa = Helper.CreaeteFileAndAddText(curDir, "a");

                //ACT
                var bfl = new FileCopyLog(dest);
                var q1 = await bfl.ReadAsync();
                var cf = bfl.CheckFile(fa);
                var fa_ = fa.CopyToDir(dest);
                bfl.AddFile(fa);
                await bfl.SaveCopyLogAsync();

                var fb = Helper.CreaeteFileAndAddText(curDir, "b");
                var fb_ = fb.CopyToDir(dest);
                var bfl2 = new FileCopyLog(dest);
                var q2 = await bfl2.ReadAsync();

                var cf_fa = bfl2.CheckFile(fa);
                var cf_fb = bfl2.CheckFile(fb);
                bfl2.AddFile(fb);
                await bfl2.SaveCopyLogAsync();

                var bfl3 = new FileCopyLog(dest);
                var q3 = await bfl3.ReadAsync();




                //ASSERT
                Xunit.Assert.True(cf == CheckFileResult.New);
                Xunit.Assert.True(cf_fa == CheckFileResult.Same);
                Xunit.Assert.True(cf_fb == CheckFileResult.New);
                Xunit.Assert.True(q1 == 0);
                Xunit.Assert.True(q2 == 1);
                Xunit.Assert.True(q3 == 2);
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                curDir.Delete(true);
                dest.Delete(true);
            }
           

        }
    }
}