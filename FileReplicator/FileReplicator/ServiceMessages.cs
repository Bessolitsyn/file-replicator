using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("FileReplicatorTests")]

namespace FileReplicator
{
    /// <summary>
    /// Provides a set of static messages and result codes used for synchronization operations and error reporting.
    /// <para>Предоставляет набор статических сообщений и кодов результатов, используемых для операций синхронизации и отчетности об ошибках.</para>
    /// </summary>
    public static class ServiceMessages
    {
        /// <summary>
        /// Codes indicating the result of a file synchronization operation.
        /// <para>Коды, указывающие на результат операции синхронизации файлов.</para>
        /// </summary>
        public enum SyncOperationResultsCode : int
        {
            /// <summary> File was successfully copied to a new location. / Файл был успешно скопирован в новое местоположение. </summary>
            SuccessfulCopying = 0,
            /// <summary> File was successfully overwritten in the destination. / Файл был успешно перезаписан в назначении. </summary>
            SuccessfulOverwriting = 1,
            /// <summary> An error occurred while copying the file. / Произошла ошибка при копировании файла. </summary>
            FailCopying = 2,
            /// <summary> An error occurred while overwriting the file. / Произошла ошибка при перезаписи файла. </summary>
            FailOverwriting = 3,
            /// <summary> No action was taken because files were identical. / Действие не было предпринято, так как файлы идентичны. </summary>
            NoAction = 4,
            /// <summary> The file is locked by another process and cannot be accessed. / Файл заблокирован другим процессом и недоступен. </summary>
            FileLocked = 5,
            /// <summary> File was successfully deleted from the destination. / Файл был успешно удален из назначения. </summary>
            SuccessfulDeleting = 6,
            /// <summary> File was successfully renamed in the destination. / Файл был успешно переименован в назначении. </summary>
            SuccessfulRenamed = 7,
            /// <summary> File was successfully moved in the destination. / Файл был успешно перемещен в назначении. </summary>
            SuccessfulMoved= 8
        }
        
        /// <summary>
        /// Human-readable string representations of the <see cref="SyncOperationResultsCode"/> values.
        /// <para>Человекочитаемые строковые представления значений <see cref="SyncOperationResultsCode"/>.</para>
        /// </summary>
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

        /// <summary>
        /// Codes for common exception scenarios.
        /// <para>Коды для распространенных сценариев исключений.</para>
        /// </summary>
        public enum ExceptionMessageCode : int
        {
            /// <summary> Settings file is missing or invalid. / Файл настроек отсутствует или недействителен. </summary>
            NoSettings = 0,
            /// <summary> The specified destination folder does not exist. / Указанная папка назначения не существует. </summary>
            NoDestinationFolder=1
        }

        /// <summary>
        /// Human-readable error messages corresponding to <see cref="ExceptionMessageCode"/>.
        /// <para>Человекочитаемые сообщения об ошибках, соответствующие <see cref="ExceptionMessageCode"/>.</para>
        /// </summary>
        public static string[] ExceptionMessages { get; } = [
            "No settings.json file or serilization error or required property is empty",
            "Destination folder doesn't exist"
        ];


    }
}
