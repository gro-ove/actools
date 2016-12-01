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
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers {
    public class ContentCategory {
        public const string Miscellaneous = "Miscellaneous";
        public const string BrandBadges = "Brand Badges";
        public const string CarCategories = "Car Categories";
        public const string TrackCategories = "Track Categories";
        public const string UpgradeIcons = "Upgrade Icons";
        public const string GridTypes = "Grid Types";
    }

    public class FilesStorage : AbstractFilesStorage {
        public const string DataDirName = "Data", DataUserDirName = "Data (User)";

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

        public class ContentEntry {
            internal ContentEntry(string filename, bool userFile) {
                Filename = filename;
                Name = UnescapeString(Path.GetFileNameWithoutExtension(filename));
                UserFile = userFile;
            }

            public string Filename { get; }

            [Localizable(false)]
            public string Name { get; }

            public bool UserFile { get; }

            public bool Exists => File.Exists(Filename);
        }

        public ContentEntry GetContentFile(params string[] name) {
            var nameJoined = Path.Combine(name);

            var contentFile = Combine(DataDirName, nameJoined);
            var contentUserFile = Combine(DataUserDirName, nameJoined);
            
            EnsureDirectory(Path.GetDirectoryName(contentFile));
            EnsureDirectory(Path.GetDirectoryName(contentUserFile));

            var isOverrided = File.Exists(contentUserFile);
            return new ContentEntry(isOverrided ? contentUserFile : contentFile, isOverrided);
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

        public IEnumerable<ContentEntry> GetContentDirectoryFiltered(string searchPattern, params string[] name) {
            var nameJoined = Path.Combine(name);
            var contentDir = EnsureDirectory(DataDirName, nameJoined);
            var contentUserDir = EnsureDirectory(DataUserDirName, nameJoined);

            var contentUserFiles = Directory.GetFiles(contentUserDir, searchPattern).Select(x => new ContentEntry(x, true)).ToList();
            var temp = contentUserFiles.Select(x => x.Name);

            return Directory.GetFiles(contentDir, searchPattern).Select(x => new ContentEntry(x, false))
                .Where(x => !temp.Contains(x.Name)).Concat(contentUserFiles).OrderBy(x => x.Name);
        }

        public IEnumerable<ContentEntry> GetContentDirectory(params string[] name) {
            return GetContentDirectoryFiltered(@"*", name);
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

        public string GetTemporaryFilename([Localizable(false)] string filename) {
            return GetFilename("Temporary", filename);
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
