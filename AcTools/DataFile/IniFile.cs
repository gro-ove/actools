using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AcTools.AcdFile;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class IniFile : AbstractDataFile, IEnumerable<KeyValuePair<string, IniFileSection>> {
        public IniFile(string carDir, string filename, Acd loadedAcd) : base(carDir, filename, loadedAcd) { }
        public IniFile(string carDir, string filename) : base(carDir, filename) { }
        public IniFile(string filename) : base(filename) { }
        public IniFile() { }

        public readonly Dictionary<string, IniFileSection> Content = new Dictionary<string, IniFileSection>();

        public Dictionary<string, IniFileSection>.KeyCollection Keys => Content.Keys;

        public Dictionary<string, IniFileSection>.ValueCollection Values => Content.Values;

        public IEnumerator<KeyValuePair<string, IniFileSection>> GetEnumerator() {
            return Content.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        [NotNull]
        public IniFileSection this[[LocalizationRequired(false)] string key] {
            get {
                IniFileSection result;
                if (Content.TryGetValue(key, out result)) return result;

                result = new IniFileSection();
                Content[key] = result;
                return result;
            }
            set { Content[key] = value; }
        }

        private static Regex _splitRegex, _commentRegex;

        protected override void ParseString(string data) {
            Clear();

            if (_splitRegex == null) {
                _splitRegex = new Regex(@"\r?\n|\r|(?=\[[A-Z])", RegexOptions.Compiled);
                _commentRegex = new Regex(@"//|(?<!\S);|;(?!\S)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }

            IniFileSection currentSection = null;
            foreach (var line in _splitRegex.Split(data).Select(x => {
                var m = _commentRegex.Match(x);
                return (m.Success ? x.Substring(0, m.Index) : x).Trim();
            }).Where(x => x.Length > 0)) {
                if (line[0] == '[' && line[line.Length - 1] == ']') {
                    this[line.Substring(1, line.Length - 2)] = currentSection = new IniFileSection();
                } else if (currentSection != null) {
                    var at = line.IndexOf('=');

                    if (at < 1) continue;

                    var invalidName = false;
                    for (var i = 0; i < at; i++) {
                        var c = line[i];
                        if (!(c >= 'A' && c <= 'Z' || c == '_' || c >= '0' && c <= '9')) {
                            invalidName = true;
                        }
                    }

                    if (invalidName) continue;

                    currentSection[line.Substring(0, at)] = line.Substring(at + 1);
                }
            }
        }

        public static IniFile Parse(string text) {
            var result = new IniFile();
            result.ParseString(text);
            return result;
        }

        public override void Clear() {
            Content.Clear();
        }

        private static Regex _cleanUp;

        public override string Stringify() {
            if (_cleanUp == null) {
                _cleanUp = new Regex(@"[\[\]]|(?!\S);|;(?!\S)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            }

            var s = new StringBuilder();

            foreach (var pair in Content) {
                var section = pair.Value;
                if (section.Count == 0) continue;

                s.Append('[');
                s.Append(pair.Key);
                s.Append(']');

                var commentary = section.Commentary;
                if (commentary != null) {
                    s.Append(" ; ");
                    s.Append(commentary);
                }

                s.Append(Environment.NewLine);

                var commentaries = section.Commentaries;
                foreach (var sub in section) {
                    s.Append(sub.Key);
                    s.Append('=');
                    s.Append(_cleanUp.Replace(sub.Value, "_"));

                    if (commentaries?.TryGetValue(sub.Key, out commentary) == true) {
                        s.Append(" ; ");
                        s.Append(commentary);
                    }

                    s.Append(Environment.NewLine);
                }

                s.Append(Environment.NewLine);
            }

            return s.ToString();
        }

        public override string ToString() {
            return Stringify();
        }

        public bool ContainsKey(string key) {
            return Content.ContainsKey(key);
        }

        public void Remove(string key) {
            Content.Remove(key);
        }

        public bool IsEmptyOrDamaged() {
            return Content.Count == 0;
        }

        public static void Write(string path, string section, string key, string value) {
            Kernel32.WritePrivateProfileString(section, key, value, path);
        }

        public static void Write(string path, string section, string key, int value) {
            Kernel32.WritePrivateProfileString(section, key, value.ToString(), path);
        }

        public static void Write(string path, string section, string key, bool value) {
            Kernel32.WritePrivateProfileString(section, key, value ? "1" : "0", path);
        }

        public static void Write(string path, string section, string key, bool? value) {
            if (!value.HasValue) return;
            Kernel32.WritePrivateProfileString(section, key, value.Value ? "1" : "0", path);
        }

        [Obsolete]
        public static string Read(string path, string section, string key) {
            var rightSection = false;
            foreach (var line in File.ReadAllLines(path).Select(x => {
                var i = x.IndexOf(';');
                return i < 0 ? x.Trim() : x.Substring(0, i).Trim();
            }).Where(x => x.Length > 0)) {
                if (line[0] == '[' && line[line.Length - 1] == ']') {
                    rightSection = line.IndexOf(section, StringComparison.Ordinal) == 1 && line.Length == section.Length + 2;
                } else if (rightSection && line.StartsWith(key)) {
                    var at = line.IndexOf('=');
                    if (at >= 0 && at == key.Length) {
                        return line.Substring(at + 1);
                    }
                }
            }

            return null;
        }

        private IEnumerable<string> GetSectionNames(string prefixName, int startFrom) {
            return LinqExtension.RangeFrom(startFrom == -1 ? 0 : startFrom)
                                .Select(x => startFrom == -1 && x == 0 ? prefixName : $"{prefixName}_{x}")
                                .TakeWhile(ContainsKey);
        }

        /// <summary>
        /// Remove all sections by prefix like SECTION_0, SECTION_1, �
        /// </summary>
        /// <param name="prefixName">Prefix</param>
        /// <param name="startFrom">ID of first section</param>
        public void RemoveSections(string prefixName, int startFrom = 0) {
            foreach (var key in GetSectionNames(prefixName, startFrom)) {
                Remove(key);
            }
        }

        /// <summary>
        /// Get all sections by prefix like SECTION_0, SECTION_1, �
        /// </summary>
        /// <param name="prefixName">Prefix (e.g. �SECTION�)</param>
        /// <param name="startFrom">ID of first section (use -1 if first section is SECTION and second is SECTION_1)</param>
        public IEnumerable<IniFileSection> GetSections([Localizable(false)] string prefixName, int startFrom = 0) {
            return GetSectionNames(prefixName, startFrom).Select(key => this[key]);
        }

        public void SetCommentaries(IniCommentariesScheme iniCommentaries) {
            foreach (var group in iniCommentaries) {
                var section = this[group.Key];
                foreach (var pair in group.Value) {
                    section.SetCommentary(pair.Key, pair.Value);
                }
            }
        }
    }
    
    public class IniCommentariesScheme : Dictionary<string, Dictionary<string, string>> {
        [NotNull]
        public new Dictionary<string, string> this[string key] {
            get {
                Dictionary<string, string> result;
                if (TryGetValue(key, out result)) return result;

                result = new Dictionary<string, string>();
                base[key] = result;
                return result;
            }
            set { base[key] = value; }
        }
    }
}