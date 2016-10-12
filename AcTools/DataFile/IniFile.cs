using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private static void ParseStringFinish(IniFileSection currentSection, string data, int nonSpace, ref string key, ref int started) {
            if (key != null) {
                string value;
                if (started != -1) {
                    var length = 1 + nonSpace - started;
                    value = length < 0 ? null : data.Substring(started, length);
                } else {
                    value = "";
                }

                currentSection.SetDirect(key, value);
                key = null;
            }

            started = -1;
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
                        if (started != -1 && key == null) {
                            key = data.Substring(started, 1 + nonSpace - started);
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

        private static void Prepare(string v, StringBuilder s) {
            for (var i = 0; i < v.Length; i++) {
                var c = v[i];
                switch (c) {
                    case '[':
                    case ']':
                    case ';':
                        s.Append('_');
                        break;
                    default:
                        s.Append(c);
                        break;
                }
            }
        }

        private static string Prepare(string v) {
            var s = new StringBuilder(v.Length);
            Prepare(v, s);
            return s.ToString();
        }

        public override string Stringify() {
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
                    Prepare(sub.Value, s);

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
                return;
            }

            // so called backup
            if (backup) {
                var backupFilename = FileUtils.EnsureUnique(filename);
                File.Copy(filename, backupFilename);
                FileUtils.Recycle(backupFilename);
            }

            var data = File.ReadAllText(filename);
            var previous = Parse(data);

            // remove obsoleted sections
            data = previous.Keys.Where(x => !ContainsKey(x)).Aggregate(data, RemoveSection);

            foreach (var sectionPair in this) {
                // section in actual data existed before
                if (previous.ContainsKey(sectionPair.Key)) {
                    // write all values
                    data = sectionPair.Value.Aggregate(data, (c, p) => SetValue(c, sectionPair.Key, p.Key, Prepare(p.Value)));

                    // remove obsoleted values
                    data = previous[sectionPair.Key].Keys.Where(x => !sectionPair.Value.ContainsKey(x))
                                                    .Aggregate(data, (c, k) => RemoveValue(c, sectionPair.Key, k));
                } else {
                    // this is a new section
                    data = AddSection(data, sectionPair.Key, sectionPair.Value);
                }
            }

            File.WriteAllText(filename, data);
        }

        private static bool SetValueFinish(bool current, string kkey, string value, ref string key, ref string data, int nonSpace, ref int tokenStarted) {
            if (current && key == kkey) {
                if (tokenStarted == -1) {
                    data = data.Substring(0, nonSpace + 1) + value + data.Substring(nonSpace + 1);
                } else {
                    var length = 1 + nonSpace - tokenStarted;
                    data = data.Substring(0, tokenStarted) + value + data.Substring(length < 0 ? tokenStarted : tokenStarted + length);
                }
                return true;
            }

            tokenStarted = -1;
            key = null;
            return false;
        }

        private static string SetValue(string data, string section, string key, string value) {
            var started = -1;
            var nonSpace = -1;
            string currentKey = null;
            var current = false;
            for (var i = 0; i < data.Length; i++) {
                var c = data[i];
                switch (c) {
                    case '[':
                        if (SetValueFinish(current, key, value, ref currentKey, ref data, nonSpace, ref started)) return data;
                        if (current) {
                            return data.Substring(0, i) + (data[i - 1] == '\n' ? "" : "\n") + key + "=" + value + "\n" + data.Substring(i);
                        }

                        var s = ++i;
                        if (s == data.Length) break;
                        for (; i < data.Length && data[i] != ']'; i++) { }

                        current = data.Substring(s, i - s) == section;
                        break;

                    case '\n':
                        if (SetValueFinish(current, key, value, ref currentKey, ref data, nonSpace, ref started)) return data;
                        break;

                    case '=':
                        if (started != -1) {
                            currentKey = data.Substring(started, 1 + nonSpace - started);
                            started = -1;
                            nonSpace = i;
                        }
                        break;

                    case '/':
                        if (i + 1 < data.Length && data[i + 1] == '/') {
                            goto case ';';
                        }
                        goto default;

                    case ';':
                        if (SetValueFinish(current, key, value, ref currentKey, ref data, nonSpace, ref started)) return data;
                        do { i++; } while (i < data.Length && data[i] != '\n');
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

            if (SetValueFinish(current, key, value, ref currentKey, ref data, nonSpace, ref started)) return data;
            return data + (data.Length > 0 && data[data.Length - 1] == '\n' ? "" : "\n") + key + "=" + value;
        }

        private static string RemoveValue(string data, string section, string key) {
            return SetValue(data, section, key, "");
        }

        private static string AddSection(string data, string sectionName, IniFileSection section) {
            var s = new StringBuilder(data);

            if (data.Length > 0 && data[data.Length - 1] != '\n') {
                s.Append('\n');
            }

            s.Append($"[{sectionName}]\n");
            foreach (var pair in section) {
                s.Append(pair.Key);
                s.Append('=');
                Prepare(pair.Value, s);
                s.Append('\n');
            }

            return s.ToString();
        }

        private static string RemoveSection(string data, string section) {
            var started = -1;
            var current = false;
            for (var i = 0; i < data.Length; i++) {
                var c = data[i];
                switch (c) {
                    case '[':
                        if (current) return data.Substring(0, started) + data.Substring(i);
                        for (started = i++; i < data.Length && data[i] != ']'; i++) { }
                        current = data.Substring(started + 1, i - started - 1) == section;
                        break;
                    case '/':
                        if (i + 1 < data.Length && data[i + 1] == '/') {
                            goto case ';';
                        }
                        break;
                    case ';':
                        do { i++; } while (i < data.Length && data[i] != '\n');
                        break;
                }
            }

            return current ? data.Substring(0, started) : data;
        }
        #endregion

        protected override void SaveTo(string filename, bool backup) {
            if (OptionKeepComments) {
                SaveToKeepComments(filename, backup);
            } else {
                base.SaveTo(filename, backup);
            }
        }

        protected override Task SaveToAsync(string filename, bool backup) {
            if (OptionKeepComments) {
                SaveToKeepComments(filename, backup);
                return Task.Delay(0);
            }

            return base.SaveToAsync(filename, backup);
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