using System;
using System.IO;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public partial class FileUtils {
        [NotNull, Pure]
        public static string[] Split(string s) {
            return s.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool ArePathsEqual(string pathA, string pathB) {
            return NormalizePath(pathA).Equals(NormalizePath(pathB), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// </summary>
        /// <param name="filename">Ex.: C:\Windows\System32\explorer.exe</param>
        /// <param name="directory">Ex.: C:\Windows</param>
        /// <returns>System32\explorer.exe</returns>
        [NotNull, Pure]
        public static string GetRelativePath([NotNull] string filename, [NotNull] string directory) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (directory == null) throw new ArgumentNullException(nameof(directory));

            filename = NormalizePath(filename);
            directory = NormalizePath(directory);

            if (!filename.StartsWith(directory, StringComparison.OrdinalIgnoreCase) || directory.Length == 0) return filename;

            var result = filename.Substring(directory[directory.Length - 1].IsDirectorySeparator() ? directory.Length - 1 : directory.Length);
            return result.Length == 0 ? string.Empty : !result[0].IsDirectorySeparator() ? filename : result.Substring(1);
        }

        [NotNull, Pure]
        public static string GetFullPath([NotNull] string filename, [NotNull] string relativeTo) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (relativeTo == null) throw new ArgumentNullException(nameof(relativeTo));
            if (Path.IsPathRooted(filename)) return filename;
            return Path.GetFullPath(Path.Combine(relativeTo, filename));
        }

        [NotNull, Pure]
        public static string GetFullPath([NotNull] string filename, [NotNull] Func<string> lazyRelativeTo) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (lazyRelativeTo == null) throw new ArgumentNullException(nameof(lazyRelativeTo));
            if (Path.IsPathRooted(filename)) return filename;
            return Path.GetFullPath(Path.Combine(lazyRelativeTo(), filename));
        }

        /// <summary>
        /// </summary>
        /// <param name="filename">Ex.: C:\Windows\System32\explorer.exe</param>
        /// <param name="directory">Ex.: C:\Windows</param>
        /// <returns>System32\explorer.exe</returns>
        [CanBeNull, Pure]
        public static string TryToGetRelativePath([NotNull] string filename, [NotNull] string directory) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (directory == null) throw new ArgumentNullException(nameof(directory));

            filename = filename.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            directory = directory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (directory.Length == 0) return filename;
            if (!filename.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) return null;

            var result = filename.Substring(directory[directory.Length - 1].IsDirectorySeparator() ? directory.Length - 1 : directory.Length);
            return result.Length == 0 ? string.Empty : !result[0].IsDirectorySeparator() ? null : result.Substring(1);
        }
    }

    public static class CharExtension {
        [Pure]
        public static bool IsDirectorySeparator(this char c) {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }
    }
}
