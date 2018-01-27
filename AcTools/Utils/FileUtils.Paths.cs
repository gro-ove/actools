using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public static partial class FileUtils {
        #region For compatibility
        [NotNull, Pure, UsedImplicitly, Obsolete]
        public static string GetSfxGuidsFilename(string acRoot) {
            return AcPaths.GetSfxGuidsFilename(acRoot);
        }
        #endregion

        [NotNull, Pure]
        public static string[] Split(string s) {
            return s.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        [NotNull]
        public static string NormalizePath([NotNull] string filename) {
            var result = new StringBuilder(filename.Length);
            bool lastSep = false, allGood = true;
            for (var i = 0; i < filename.Length; i++){
                var c = filename[i];
                switch (c){
                    case '.':
                        if ((i == 0 || lastSep) && (i >= filename.Length - 1 || IsSeparator(filename[i + 1]))){
                            i++;
                            allGood = false;
                            break;
                        } else if (lastSep && filename[i + 1] == '.' && (i >= filename.Length - 2 || IsSeparator(filename[i + 2]))) {
                            int j;
                            for (j = Math.Max(result.Length - 2, 0); j > 0 && result[j] != '\\'; j--){}
                            result.Remove(j, result.Length - (j == 0 ? 0 : j + 1));
                            i += 2;
                            allGood = false;
                            break;
                        }
                        goto default;
                    case '/':
                        allGood = false;
                        goto case '\\';
                    case '\\':
                        if (!lastSep){
                            lastSep = true;
                            result.Append('\\');
                        }
                        break;
                    default:
                        lastSep = false;
                        result.Append(c);
                        break;
                }
            }

            return result.Length > 0 && result[result.Length - 1] == '\\'
                    ? result.ToString(0, result.Length - 1)
                    : allGood ? filename : result.ToString();

            bool IsSeparator(char c){
                return c == '/' || c == '\\';
            }
        }

        /// <summary>
        /// Is A in any way a parent of B?
        /// </summary>
        /// <param name="parent">For example, “C:\Windows”</param>
        /// <param name="child">For example, “c:/windows/system32”</param>
        /// <returns>For example, true</returns>
        public static bool Affects([NotNull] string parent, [NotNull] string child) {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (child == null) throw new ArgumentNullException(nameof(child));

            parent = NormalizePath(parent);
            child = NormalizePath(child);

            if (string.Equals(parent, child, StringComparison.OrdinalIgnoreCase)) return true;
            if (!child.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) return false;

            var s = child.SubstringExt(parent.Length);
            return s.Length > 0 && s[0] == Path.DirectorySeparatorChar;
        }

        public static bool ArePathsEqual(string pathA, string pathB) {
            return NormalizePath(pathA).Equals(NormalizePath(pathB), StringComparison.OrdinalIgnoreCase);
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
        /// Might use “..” in the result.
        /// </summary>
        /// <param name="filename">Ex.: C:\Windows\System32\explorer.exe</param>
        /// <param name="directory">Ex.: C:\ProgramData</param>
        /// <returns>..\System32\explorer.exe</returns>
        [NotNull, Pure]
        public static string GetRelativePath([NotNull] string filename, [NotNull] string directory) {
            filename = NormalizePath(filename);
            directory = NormalizePath(directory);

            try {
                var builder = new StringBuilder(1024);
                var result = PathRelativePathTo(builder, NormalizePath(directory) + '\\', 0, NormalizePath(filename), 0);
                return result ? builder.ToString().ApartFromFirst(@".\") : filename;
            } catch {
                return filename;
            }
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern bool PathRelativePathTo(StringBuilder lpszDst, string from, uint attrFrom, string to, uint attrTo);

        /// <summary>
        /// Doesn’t use .. in the result.
        /// </summary>
        /// <param name="filename">Ex.: C:\Windows\System32\explorer.exe</param>
        /// <param name="directory">Ex.: C:\Windows</param>
        /// <returns>System32\explorer.exe</returns>
        [CanBeNull, Pure]
        public static string GetPathWithin([NotNull] string filename, [NotNull] string directory) {
            filename = NormalizePath(filename);
            directory = NormalizePath(directory);

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