using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileReplicator
{
    /// <summary>
    /// Provides extension methods for <see cref="FileInfo"/>.
    /// <para>Предоставляет методы расширения для <see cref="FileInfo"/>.</para>
    /// </summary>
    public static class FileInfoExtensions
    {
        /// <summary>
        /// Copies the current file into the specified directory.
        /// <para>Копирует текущий файл в указанный каталог.</para>
        /// </summary>
        /// <param name="fileInfo">The file information. / Информация о файле.</param>
        /// <param name="dir">The target directory. / Целевой каталог.</param>
        /// <returns>The resulting <see cref="FileInfo"/> of the copied file. / Результирующий <see cref="FileInfo"/> скопированного файла.</returns>
        public static FileInfo CopyToDir(this FileInfo fileInfo, DirectoryInfo dir)
        {
            return fileInfo.CopyTo(Path.Combine(dir.FullName, fileInfo.Name));
        }
    }
}
