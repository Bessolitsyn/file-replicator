using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("FileReplicatorTests")]

namespace FileReplicator
{
    public static class ServiceMessages
    {
        public enum SyncOperationResultsCode : int
        {
            SuccessfulCopying = 0,
            SuccessfulOverwriting = 1,
            FailCopying = 2,
            FailOverwriting = 3,
            NoAction = 4,
            FileLocked = 5,
            SuccessfulDeleting = 6,
            SuccessfulRenamed = 7,
            SuccessfulMoved= 8
        }
        public static string[] LogMessages { get; } = [
            "SuccessfulCopying",
            "SuccessfulOverwriting",
            "FailCopying",
            "FailOverwriting",
            "NoAction",
            "FileLocked",
            "SuccessfulDeleting",
            "SuccessfulRenamed",
            "SuccessfulMoved"
        ];

        public enum ExceptionMessageCode : int
        {
            NoSettings = 0,
            NoDestinationFolder=1
        }

        public static string[] ExceptionMessages { get; } = [
            "No settings.json file or serilization error or required property is empty",
            "Destination folder doesn't exist"
        ];


    }
}
