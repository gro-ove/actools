using AcTools.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Local

namespace AcTools.Utils {
    public partial class FileUtils {
        #region Recycle Bin
        [Flags]
        public enum FileOperationFlags : ushort {
            FOF_SILENT = 0x0004,
            FOF_NOCONFIRMATION = 0x0010,
            FOF_ALLOWUNDO = 0x0040,
            FOF_SIMPLEPROGRESS = 0x0100,
            FOF_NOERRORUI = 0x0400,
            FOF_WANTNUKEWARNING = 0x4000,
        }

        public enum FileOperationType : uint {
            FO_MOVE = 0x0001,
            FO_COPY = 0x0002,
            FO_DELETE = 0x0003,
            FO_RENAME = 0x0004,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 1)]
        private struct SHFILEOPSTRUCT {
            public readonly IntPtr hwnd;

            [MarshalAs(UnmanagedType.U4)]
            public FileOperationType wFunc;

            public string pFrom;
            public readonly string pTo;
            public FileOperationFlags fFlags;

            [MarshalAs(UnmanagedType.Bool)]
            public readonly bool fAnyOperationsAborted;

            public readonly IntPtr hNameMappings;
            public readonly string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        private static bool DeleteFile(string[] path, FileOperationFlags flags) {
            path = path?.Where(Exists).ToArray();
            if (path == null || path.Length == 0 || path.All(x => x == null)) return false;
            try {
                var fs = new SHFILEOPSTRUCT {
                    wFunc = FileOperationType.FO_DELETE,
                    pFrom = string.Join("\0", path.Where(x => x != null)) + "\0\0",
                    fFlags = flags
                };
                SHFileOperation(ref fs);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        public static bool RecycleVisible([CanBeNull] params string[] path) {
            return DeleteFile(path, FileOperationFlags.FOF_ALLOWUNDO | FileOperationFlags.FOF_NOCONFIRMATION |
                                    FileOperationFlags.FOF_WANTNUKEWARNING);
        }

        public static bool Recycle([CanBeNull] params string[] path) {
            return DeleteFile(path, FileOperationFlags.FOF_ALLOWUNDO | FileOperationFlags.FOF_NOCONFIRMATION |
                                    FileOperationFlags.FOF_NOERRORUI | FileOperationFlags.FOF_SILENT);
        }

        public static bool DeleteSilent([CanBeNull] params string[] path) {
            return DeleteFile(path, FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_NOERRORUI |
                                    FileOperationFlags.FOF_SILENT);
        }

        public class RecycleOriginalHolder : IDisposable {
            private readonly string _filename;

            internal RecycleOriginalHolder(string filename) {
                _filename = filename;
                Filename = EnsureUnique(_filename, ".tmp-{0}");
            }

            public string Filename { get; }

            public void Dispose() {
                if (ArePathsEqual(Filename, _filename)) return;
                if (File.Exists(Filename)) {
                    Recycle(_filename);
                    File.Move(Filename, _filename);
                }
            }
        }

        public static RecycleOriginalHolder RecycleOriginal(string filename) {
            return new RecycleOriginalHolder(filename);
        }

        public static bool Undo() {
            var handle = User32.FindWindowEx(
                    User32.FindWindowEx(User32.FindWindow("Progman", "Program Manager"), IntPtr.Zero, "SHELLDLL_DefView", ""),
                    IntPtr.Zero, "SysListView32", "FolderView");
            if (handle != IntPtr.Zero) {
                var current = User32.GetForegroundWindow();
                User32.SetForegroundWindow(handle);
                SendKeys.SendWait("^(z)");
                User32.SetForegroundWindow(current);
                return true;
            } else {
                return false;
            }
        }
        #endregion

        public static void EnsureFileDirectoryExists(string filename) {
            EnsureDirectoryExists(Path.GetDirectoryName(filename));
        }

        public static void EnsureDirectoryExists(string directory) {
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
        }

        public static string FastChecksum(string filename) {
            var blockSize = 500;
            byte[] result;

            var md5 = MD5.Create();
            using (var file = File.OpenRead(filename)) {
                if (file.Length < blockSize * 3) {
                    result = md5.ComputeHash(file);
                } else {
                    var temp = new byte[blockSize * 3];
                    file.Read(temp, 0, blockSize);
                    file.Seek((file.Length - blockSize / 2), SeekOrigin.Begin);
                    file.Read(temp, blockSize, blockSize);
                    file.Seek(-blockSize, SeekOrigin.End);
                    file.Read(temp, blockSize * 2, blockSize);

                    result = md5.ComputeHash(temp, 0, temp.Length);
                }
            }

            var sb = new StringBuilder();
            foreach (var t in result) {
                sb.Append(t.ToString("X2"));
            }
            return sb.ToString();
        }

        public static string ReadableSize(long size) {
            double temp = size;
            var level = 0;
            while (temp > 2e3) {
                temp /= 1024;
                level++;
            }

            return temp.ToString("F2") + " " + (" KMGT"[level] + "B").TrimStart();
        }

        public static void Unzip(string pathToZip, string destination) {
            if (!Directory.Exists(destination)) {
                Directory.CreateDirectory(destination);
            }

            using (var archive = ZipFile.OpenRead(pathToZip)) {
                archive.ExtractToDirectory(destination);
            }
        }

        [Localizable(false), NotNull]
        public static string[] GetFilesSafe([NotNull] string path, string searchPattern = null, SearchOption searchOption = SearchOption.TopDirectoryOnly) {
            return Directory.Exists(path) ? searchPattern == null ? Directory.GetFiles(path) : Directory.GetFiles(path, searchPattern, searchOption) :
                    new string[0];
        }

        [NotNull]
        public static IEnumerable<string> GetFilesRecursive(string path, string searchPattern = null) {
            var queue = new Queue<string>();
            queue.Enqueue(path);

            while (queue.Count > 0) {
                path = queue.Dequeue();

                string[] dirs = null;
                try {
                    dirs = Directory.GetDirectories(path);
                } catch (Exception) {
                    // ignored
                }

                if (dirs != null) {
                    foreach (var t in from t in dirs
                                      let attributes = new DirectoryInfo(t).Attributes
                                      where (attributes & (FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System)) == 0
                                      select t) {
                        queue.Enqueue(t);
                    }
                }

                string[] files = null;
                try {
                    files = searchPattern == null ? Directory.GetFiles(path) : Directory.GetFiles(path, searchPattern);
                } catch (Exception) {
                    // ignored
                }

                if (files == null) continue;

                foreach (var t in files) {
                    yield return t;
                }
            }
        }

        public static IEnumerable<string> GetFilesAndDirectories(string directory) {
            foreach (var dir in Directory.GetDirectories(directory)) {
                yield return dir;
            }

            foreach (var file in Directory.GetFiles(directory)) {
                yield return file;
            }
        }

        /// <summary>
        /// Move directory or file
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public static void Move(string from, string to) {
            if (string.Equals(from, to, StringComparison.Ordinal)) return;

            EnsureFileDirectoryExists(to);
            if (File.GetAttributes(from).HasFlag(FileAttributes.Directory)) {
                Directory.Move(from, to);
            } else {
                File.Move(from, to);
            }
        }

        /// <summary>
        /// Copy directory or file
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public static void Copy(string from, string to) {
            if (string.Equals(from, to, StringComparison.Ordinal)) return;

            EnsureFileDirectoryExists(to);
            if (File.GetAttributes(from).HasFlag(FileAttributes.Directory)) {
                CopyRecursive(from, to);
            } else {
                File.Copy(from, to, true);
            }
        }

        /// <summary>
        /// Helps to find original casing.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string GetOriginalFilename(string filename) {
            var directory = Path.GetDirectoryName(filename);
            var name = Path.GetFileName(filename);
            if (directory == null) return filename;
            return Directory.GetFiles(directory, name).FirstOrDefault() ??
                    Directory.GetDirectories(directory, name).FirstOrDefault() ?? filename;
        }

        /// <summary>
        /// Move directory or file (without changing the name!)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public static void ChangeDirectory(string from, string to) {
            if (string.Equals(Path.GetDirectoryName(from), Path.GetDirectoryName(to), StringComparison.OrdinalIgnoreCase)) return;
            Move(from, to);
        }

        /// <summary>
        /// If directory or file exists.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static bool Exists(string location) {
            return File.Exists(location) || Directory.Exists(location);
        }

        /// <summary>
        /// How should I call it?
        /// </summary>
        /// <param name="filename"></param>
        public static IDisposable RestoreLater(string filename) {
            return new RestorationWrapper(filename);
        }

        public static IDisposable TemporaryRemove(string filename) {
            var wrapper = new RestorationWrapper(filename);
            if (File.Exists(filename)) File.Delete(filename);
            return wrapper;
        }

        private class RestorationWrapper : IDisposable {
            private readonly string _filename;
            private readonly byte[] _bytes;

            internal RestorationWrapper(string filename) {
                try {
                    _filename = filename;
                    _bytes = File.Exists(filename) ? File.ReadAllBytes(_filename) : null;
                } catch (Exception) {
                    // ignored
                }
            }

            void IDisposable.Dispose() {
                try {
                    if (_bytes != null) {
                        File.WriteAllBytes(_filename, _bytes);
                    } else if (File.Exists(_filename)) {
                        File.Delete(_filename);
                    }
                } catch (Exception) {
                    // ignored
                }
            }
        }

        public static string GetTempFileName(string dir) {
            var i = 0;
            string result;
            do {
                result = Path.Combine(dir, "__tmp_" + i++);
            } while (Exists(result));
            return result;
        }

        public static string GetTempFileNameFixed(string dir, string fixedName) {
            var d = GetTempFileName(dir);
            Directory.CreateDirectory(d);
            return Path.Combine(d, fixedName);
        }

        public static string GetTempFileName(string dir, string extension) {
            var i = 0;
            string result;
            do {
                result = Path.Combine(dir, "__tmp_" + i++ + extension);
            } while (Exists(result));
            return result;
        }

        public static string NormalizePath(string filename) {
            return filename.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries).JoinToString(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Is A in any way a parent of B?
        /// </summary>
        /// <param name="parent">For example, “C:\Windows”</param>
        /// <param name="child">For example, “c:/windows/system32”</param>
        /// <returns>For example, true</returns>
        public static bool IsAffected([NotNull] string parent, [NotNull] string child) {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (child == null) throw new ArgumentNullException(nameof(child));

            parent = NormalizePath(parent);
            child = NormalizePath(child);

            if (string.Equals(parent, child, StringComparison.OrdinalIgnoreCase)) return true;
            if (!child.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) return false;

            var s = child.SubstringExt(parent.Length);
            return s.Length > 0 && s[0] == Path.DirectorySeparatorChar;
        }

        private static bool TryToHardlink([NotNull] string source, [NotNull] string destination, bool overwrite = false) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            if (overwrite && File.Exists(destination)) {
                File.Delete(destination);
            }

            return Kernel32.CreateHardLink(destination, source, IntPtr.Zero);
        }

        [Obsolete]
        public static void Hardlink([NotNull] string source, [NotNull] string destination, bool overwrite = false) {
            if (!TryToHardlink(source, destination, overwrite)) {
                throw new Exception("Can’t make a hardlink");
            }
        }

        public static void HardlinkOrCopy([NotNull] string source, [NotNull] string destination, bool overwrite = false) {
            if (!TryToHardlink(source, destination, overwrite)) {
                File.Copy(source, destination, overwrite);
            }
        }

        /// <summary>
        /// Check if filename is a directory.
        /// </summary>
        /// <param name="filename"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <returns></returns>
        public static bool IsDirectory([NotNull] string filename) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            return (File.GetAttributes(filename) & FileAttributes.Directory) == FileAttributes.Directory;
        }

        public static string EnsureFileNameIsValid(string fileName) {
            return Path.GetInvalidFileNameChars().Union("[]").Aggregate(fileName, (current, c) => current.Replace(c, '-'));
        }

        [NotNull, Localizable(false)]
        public static string EnsureUnique([NotNull] string filename, [NotNull] string postfix, bool forcePostfix, int startFrom = 1) {
            if (!forcePostfix && !Exists(filename)) return filename;

            var ext = Path.GetExtension(filename) ?? "";
            var start = filename.Substring(0, filename.Length - ext.Length);

            for (var i = startFrom; i < 99999; i++) {
                var result = start + string.Format(postfix, i) + ext;
                if (!Exists(result)) return result;
            }

            throw new Exception("Can’t find unique filename");
        }

        [NotNull, Localizable(false)]
        public static string EnsureUnique([NotNull] string filename, [NotNull] string postfix = "-{0}") {
            return EnsureUnique(filename, postfix, false);
        }

        public static void CopyRecursive(string source, string destination) {
            if (File.GetAttributes(source).HasFlag(FileAttributes.Directory)) {
                Directory.CreateDirectory(destination);

                foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) {
                    Directory.CreateDirectory(Path.Combine(destination, GetRelativePath(dirPath, source)));
                }

                foreach (var filePath in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) {
                    File.Copy(filePath, Path.Combine(destination, GetRelativePath(filePath, source)), true);
                }
            } else {
                File.Copy(source, destination, true);
            }
        }

        [Obsolete]
        public static void HardlinkRecursive(string source, string destination) {
            if (File.GetAttributes(source).HasFlag(FileAttributes.Directory)) {
                Directory.CreateDirectory(destination);

                foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) {
                    Directory.CreateDirectory(Path.Combine(destination, GetRelativePath(dirPath, source)));
                }

                foreach (var filePath in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) {
                    Hardlink(filePath, Path.Combine(destination, GetRelativePath(filePath, source)), true);
                }
            } else {
                Hardlink(source, destination, true);
            }
        }

        public static void HardlinkOrCopyRecursive(string source, string destination) {
            if (File.GetAttributes(source).HasFlag(FileAttributes.Directory)) {
                Directory.CreateDirectory(destination);

                foreach (var dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) {
                    Directory.CreateDirectory(Path.Combine(destination, GetRelativePath(dirPath, source)));
                }

                foreach (var filePath in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) {
                    HardlinkOrCopy(filePath, Path.Combine(destination, GetRelativePath(filePath, source)), true);
                }
            } else {
                HardlinkOrCopy(source, destination, true);
            }
        }
    }
}
