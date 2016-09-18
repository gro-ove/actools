using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AcTools.AcdFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class IniFile : AbstractDataFile, IEnumerable<KeyValuePair<string, IniFileSection>> {
        /// <summary>
        /// Saving is much slower when enabled.
        /// </summary>
        public static bool OptionKeepComments = false;

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

        private static void ParseStringFinish(IniFileSection currentSection, string data, int nonSpace, ref string key, ref int tokenStarted) {
            if (key != null) {
                string value;
                if (tokenStarted != -1) {
                    var length = 1 + nonSpace - tokenStarted;
                    value = length < 0 ? null : data.Substring(tokenStarted, length);
                } else {
                    value = null;
                }

                currentSection.SetDirect(key, value);
                key = null;
            }

            tokenStarted = -1;
        }

        protected override void ParseString(string data) {
            Clear();

            IniFileSection currentSection = null;
            var started = -1;
            var nonSpace = -1;
            string key = null;
            for (var i = 0; i < data.Length; i++) {
                var c = data[i];
                switch (c) {
                    case '[':
                        ParseStringFinish(currentSection, data, nonSpace, ref key, ref started);

                        var s = ++i;
                        if (s == data.Length) break;
                        for (; i < data.Length && data[i] != ']'; i++) { }

                        this[data.Substring(s, i - s)] = currentSection = new IniFileSection();
                        break;

                    case '\n':
                        ParseStringFinish(currentSection, data, nonSpace, ref key, ref started);
                        break;

                    case '=':
                        if (started != -1) {
                            key = data.Substring(started, i - started);
                            started = -1;
                        }
                        break;

                    case '/':
                        if (i + 1 < data.Length && data[i + 1] == '/') {
                            goto case ';';
                        }
                        goto default;

                    case ';':
                        ParseStringFinish(currentSection, data, nonSpace, ref key, ref started);
                        for (i++; i < data.Length && data[i] != '\n'; i++) { }
                        break;

                    default:
                        if (!char.IsWhiteSpace(c)) {
                            nonSpace = i;
                            if (started == -1) {
                                started = i;
                            }
                        }
                        break;
                }
            }

            ParseStringFinish(currentSection, data, nonSpace, ref key, ref started);
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
                _cleanUp = new Regex(@"[\[\];]", RegexOptions.Compiled);
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

        #region Shitty comment-keeping saving
        /// <summary>
        /// This is the shittiest form of saving I can think of.
        /// </summary>
        /// <param name="filename">Destination.</param>
        /// <param name="backup">Move original file to Recycle Bin.</param>
        private void SaveToKeepComments(string filename, bool backup) {
            // if there is no original file — there is no comments, save it as usual
            if (!File.Exists(filename)) {
                File.WriteAllText(filename, Stringify());
            }

            // so called backup
            if (backup) {
                var backupFilename = FileUtils.EnsureUnique(filename);
                File.Copy(filename, backupFilename);
                FileUtils.Recycle(backupFilename);
            }

            var previousText = File.ReadAllText(filename);
            var previousParsed = Parse(previousText);
            var lines = previousText.Replace("\r", "").Split('\n').ToList();

            foreach (var removedSectionKey in previousParsed.Keys.Where(x => !ContainsKey(x))) {
                RemoveSection(lines, removedSectionKey);
            }

            foreach (var sectionPair in this) {
                if (previousParsed.ContainsKey(sectionPair.Key)) {
                    // section in actual data existed before
                    foreach (var valuePair in sectionPair.Value) {
                        // write all values
                        SetValue(lines, sectionPair.Key, valuePair.Key, valuePair.Value);
                    }

                    foreach (var removedValueKey in previousParsed[sectionPair.Key].Keys.Where(x => !sectionPair.Value.ContainsKey(x))) {
                        // remove obsoleted values
                        RemoveValue(lines, sectionPair.Key, removedValueKey);
                    }
                } else {
                    // this is a new section
                    AddSection(lines, sectionPair.Key, sectionPair.Value);
                }
            }
        }

        private static void SetValue(List<string> list, string section, string key, string value) {
            throw new NotImplementedException();
        }

        private static void RemoveValue(List<string> list, string section, string key) {
            throw new NotImplementedException();
        }

        private static void AddSection(List<string> list, string sectionName, IniFileSection section) {
            if (list.Any()) {
                list.Add("");
            }

            list.Add($"[{section}]");
            list.AddRange(section.Select(pair => $"{pair.Key}={pair.Value}"));
        }

        private static void RemoveSection(List<string> list, string sectionName) {
            throw new NotImplementedException();
        }
        #endregion

        protected override void SaveTo(string filename, bool backup) {
            if (OptionKeepComments) {
                SaveToKeepComments(filename, backup);
            } else {
                base.SaveTo(filename, backup);
            }
        }

        public override string ToString() {
            return Stringify();
        }

        public bool ContainsKey(string key) {
            return Content.ContainsKey(key);
        }

        public void Remove(string key) {
            if (Content.ContainsKey(key)) {
                Content.Remove(key);
            }
        }

        public bool IsEmptyOrDamaged() {
            return Content.Count == 0;
        }

        public static void Write(string path, [Localizable(false)] string section, [Localizable(false)] string key, [Localizable(false)] string value) {
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
        /// Remove all sections by prefix like SECTION_0, SECTION_1, …
        /// </summary>
        /// <param name="prefixName">Prefix</param>
        /// <param name="startFrom">ID of first section</param>
        public void RemoveSections([Localizable(false)] string prefixName, int startFrom = 0) {
            foreach (var key in GetSectionNames(prefixName, startFrom)) {
                Remove(key);
            }
        }

        /// <summary>
        /// Get all sections by prefix like SECTION_0, SECTION_1, …
        /// </summary>
        /// <param name="prefixName">Prefix (e.g. “SECTION”)</param>
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

        public IniFile Clone() {
            // TODO: optimization
            return Parse(Stringify());
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