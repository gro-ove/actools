using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers {
    public class FilesStorage : AbstractFilesStorage {
        public static readonly string DataDirName = "Data", DataUserDirName = "Data (User)";

        private static FilesStorage _instance;

        public static FilesStorage Instance => _instance ?? (_instance = new FilesStorage());

        public static void Initialize(string path) {
            Debug.Assert(_instance == null);
            _instance = new FilesStorage(path);
        }

        private static string DefaultDataLocation {
            get {
                var exeFilename = System.Reflection.Assembly.GetExecutingAssembly().Location;
                return Path.Combine(Path.GetDirectoryName(exeFilename) ?? @".", Path.GetFileName(exeFilename) + " Data");
            }
        }

        protected FilesStorage(string path = null) : base(path ?? DefaultDataLocation) {
            EnsureDirectory();
            EnsureDirectory(DataDirName);
            EnsureDirectory(DataUserDirName);
        }

        public override ContentWatcher Watcher(params string[] name) {
            return base.Watcher(Combine(DataUserDirName, Path.Combine(name)));
        }

        public class ContentEntry : NotifyPropertyChanged {
            private readonly bool _isDirectory;

            internal ContentEntry(string filename, bool userFile, bool isDirectory) {
                _isDirectory = isDirectory;
                Filename = filename;
                Name = UnescapeString(_isDirectory ? Path.GetFileName(filename) : Path.GetFileNameWithoutExtension(filename));
                UserFile = userFile;
            }

            public string Filename { get; }

            private DateTime? _lastWrite;

            public DateTime LastWriteTime => _lastWrite ?? (_lastWrite = Exists ? File.GetLastWriteTime(Filename) : default(DateTime)).Value;

            [Localizable(false)]
            public string Name { get; }

            public bool UserFile { get; }

            public bool Exists => _isDirectory ? Directory.Exists(Filename) : File.Exists(Filename);
        }

        [NotNull]
        public ContentEntry GetContentFile(params string[] name) {
            var nameJoined = Path.Combine(name);

            var contentFile = Combine(DataDirName, nameJoined);
            var contentUserFile = Combine(DataUserDirName, nameJoined);

            EnsureDirectory(Path.GetDirectoryName(contentFile));
            EnsureDirectory(Path.GetDirectoryName(contentUserFile));

            var isOverrided = File.Exists(contentUserFile);
            return new ContentEntry(isOverrided ? contentUserFile : contentFile, isOverrided, false);
        }

        public string LoadContentFile(string dir, [Localizable(false)] string name = null) {
            var entry = GetContentFile(dir, name);
            if (!entry.Exists) return null;

            try {
                return FileUtils.ReadAllText(entry.Filename);
            } catch (Exception exception) {
                Logging.Warning("READING FAILED: " + entry.Filename + "\n" + exception);
                return null;
            }
        }

        public JObject LoadJsonContentFile(string dir, [Localizable(false)] string name = null) {
            var entry = GetContentFile(dir, name);
            if (!entry.Exists) return null;

            try {
                return JObject.Parse(FileUtils.ReadAllText(entry.Filename));
            } catch (Exception exception) {
                Logging.Warning("JSON READING OR PARSING FAILED: " + entry.Filename + "\n" + exception);
                return null;
            }
        }

        public T LoadJsonContentFile<T>(string dir, [Localizable(false)] string name = null) {
            var entry = GetContentFile(dir, name);
            if (!entry.Exists) return default(T);

            try {
                return JsonConvert.DeserializeObject<T>(FileUtils.ReadAllText(entry.Filename));
            } catch (Exception exception) {
                Logging.Warning("JSON READING OR PARSING FAILED: " + entry.Filename + "\n" + exception);
                return default(T);
            }
        }

        public IEnumerable<ContentEntry> GetContentFilesFiltered(string searchPattern, params string[] name) {
            var nameJoined = Path.Combine(name);
            var contentDir = EnsureDirectory(DataDirName, nameJoined);
            var contentUserDir = EnsureDirectory(DataUserDirName, nameJoined);

            var contentUserFiles = Directory.GetFiles(contentUserDir, searchPattern).Select(x => new ContentEntry(x, true, false)).ToList();
            var temp = contentUserFiles.Select(x => x.Name);

            return Directory.GetFiles(contentDir, searchPattern).Select(x => new ContentEntry(x, false, false))
                .Where(x => !temp.Contains(x.Name)).Concat(contentUserFiles).OrderBy(x => x.Name);
        }

        public IEnumerable<ContentEntry> GetContentFiles(params string[] name) {
            return GetContentFilesFiltered(@"*", name);
        }

        public IEnumerable<ContentEntry> GetContentDirectoriesFiltered(string searchPattern, params string[] name) {
            var nameJoined = Path.Combine(name);
            var contentDir = EnsureDirectory(DataDirName, nameJoined);
            var contentUserDir = EnsureDirectory(DataUserDirName, nameJoined);

            var contentUserFiles = Directory.GetDirectories(contentUserDir, searchPattern).Select(x => new ContentEntry(x, true, true)).ToList();
            var temp = contentUserFiles.Select(x => x.Name);

            return Directory.GetDirectories(contentDir, searchPattern).Select(x => new ContentEntry(x, false, true))
                .Where(x => !temp.Contains(x.Name)).Concat(contentUserFiles).OrderBy(x => x.Name);
        }

        public IEnumerable<ContentEntry> GetContentDirectories(params string[] name) {
            return GetContentDirectoriesFiltered(@"*", name);
        }

        public void AddUserContentToDirectory(string name, string filename, string saveAs) {
            saveAs = EscapeString(saveAs);

            var contentUserDir = EnsureDirectory(DataUserDirName, name);
            foreach (var file in Directory.GetFiles(contentUserDir, saveAs + ".*").Where(file => Path.GetFileNameWithoutExtension(file) == saveAs)) {
                FileUtils.Recycle(file);
            }

            var destinationFilename = Path.Combine(contentUserDir, saveAs + Path.GetExtension(filename));
            File.Copy(filename, destinationFilename);
        }

        public void OpenContentDirectoryInExplorer(string name) {
            var contentUserDir = EnsureDirectory(DataUserDirName, name);
            Process.Start(contentUserDir);
        }

        [NotNull]
        public string GetTemporaryFilename([Localizable(false)] params string[] filename) {
            return GetFilename(filename.Prepend("Temporary").ToArray());
        }

        [NotNull]
        public string GetTemporaryDirectory([Localizable(false)] params string[] filename) {
            return GetDirectory(filename.Prepend("Temporary").ToArray());
        }

        private static Regex _unescapeRegex;

        private static string UnescapeString(string filename) {
            if (_unescapeRegex == null) {
                _unescapeRegex = new Regex(@"%([\dA-Z]{2})", RegexOptions.Compiled);
            }

            return _unescapeRegex.Replace(filename, x => {
                var ch = (char)int.Parse(x.Groups[1].Value, NumberStyles.HexNumber);
                switch (ch) {
                    case '%':
                    case '/':
                    case '\\':
                    case ':':
                    case '*':
                    case '?':
                    case '"':
                    case '<':
                    case '>':
                    case '|':
                        return ch.ToString();

                    default:
                        return x.Value;
                }
            });
        }

        private static string EscapeString(string filename) {
            var result = new StringBuilder(filename.Length);
            foreach (var ch in filename) {
                switch (ch) {
                    case '%':
                    case '/':
                    case '\\':
                    case ':':
                    case '*':
                    case '?':
                    case '"':
                    case '<':
                    case '>':
                    case '|':
                        result.AppendFormat(@"%{0:X2}", (int)ch);
                        break;

                    default:
                        result.Append(ch);
                        break;
                }
            }

            return result.ToString();
        }

        public void Remove(ContentEntry entry) {
            FileUtils.Recycle(entry.Filename);
        }

        protected override string GetSubdirectoryFilename(string name) {
            return name == null ? DataUserDirName : Path.Combine(DataUserDirName, name);
        }
    }
}
