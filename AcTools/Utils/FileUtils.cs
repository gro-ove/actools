using AcTools.Windows;
using System;
using System.Collections.Generic;
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
            if (path == null || path.All(x => x == null)) return false;
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

        public static bool Recycle([CanBeNull] params string[] path) {
            return DeleteFile(path, FileOperationFlags.FOF_ALLOWUNDO | FileOperationFlags.FOF_NOCONFIRMATION |
                                    FileOperationFlags.FOF_WANTNUKEWARNING);
        }

        public static bool MoveToRecycleBin([CanBeNull] params string[] path) {
            return DeleteFile(path, FileOperationFlags.FOF_ALLOWUNDO | FileOperationFlags.FOF_NOCONFIRMATION |
                                    FileOperationFlags.FOF_NOERRORUI | FileOperationFlags.FOF_SILENT);
        }

        public static bool DeleteSilent([CanBeNull] params string[] path) {
            return DeleteFile(path, FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_NOERRORUI |
                                    FileOperationFlags.FOF_SILENT);
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
                //foreach (ZipArchiveEntry entry in archive.Entries) {
                //    entry.ExtractToFile(Path.Combine(destination, entry.FullName));
                //}
            }
        }

        public static IEnumerable<string> GetFiles(string path) {
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
                    files = Directory.GetFiles(path);
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
        /// Move directory or file.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public static void Move(string from, string to) {
            if (from.Equals(to, StringComparison.Ordinal) ||
                Path.GetDirectoryName(from)?.Equals(Path.GetDirectoryName(to), StringComparison.OrdinalIgnoreCase) == true) return;
            EnsureFileDirectoryExists(to);
            if (File.GetAttributes(from).HasFlag(FileAttributes.Directory)) {
                Directory.Move(from, to);
            } else {
                File.Move(from, to);
            }
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
            string result;
            for (var i = 0; File.Exists(result = Path.Combine(dir, "__tmp_" + i)); i++) {}
            return result;
        }

        public static string GetTempFileNameFixed(string dir, string fixedName) {
            var d = GetTempFileName(dir);
            Directory.CreateDirectory(d);
            return Path.Combine(d, fixedName);
        }

        public static string GetTempFileName(string dir, string extension) {
            string result;
            for (var i = 0; File.Exists(result = Path.Combine(dir, "__tmp_" + i + extension)); i++) {}
            return result;
        }

        public static bool IsAffected([NotNull] string directory, [NotNull] string filename) {
            if (directory == null) throw new ArgumentNullException(nameof(directory));
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (string.Equals(directory, filename, StringComparison.OrdinalIgnoreCase)) return true;

            var s = filename.SubstringExt(directory.Length);
            return s.Length > 0 && (s[0] == Path.DirectorySeparatorChar || s[0] == Path.AltDirectorySeparatorChar);
        }

        public static void Hardlink([NotNull] string source, [NotNull] string destination) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            Kernel32.CreateHardLink(destination, source, IntPtr.Zero);
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
            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c, '-'));
        }

        public static string EnsureUnique(string filename) {
            if (!File.Exists(filename)) return filename;
            
            var ext = Path.GetExtension(filename) ?? "";
            var start = filename.Substring(0, filename.Length - ext.Length);

            for (var i = 1; i < 99999; i++) {
                var result = start + "-" + i + ext;
                if (!File.Exists(result)) return result;
            }

            throw new Exception("Can't find unique filename");
        }
    }
}
