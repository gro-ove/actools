using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed class PythonAppConfig : Displayable {
        internal string Filename { get; }

        private readonly string _defaultsFilename;
        private readonly IniFile _valuesIniFile;

        public event EventHandler ValueChanged;

        public bool IsResettable { get; }
        public bool IsSingleSection { get; }

        private PythonAppConfig(string filename, string appDirectory, IniFile ini, string name, IniFile values = null) {
            IsResettable = values != null;
            _valuesIniFile = values ?? ini;

            Filename = filename;
            _defaultsFilename = ini.Filename;
            if (_defaultsFilename == Filename) {
                _defaultsFilename = null;
            }

            DisplayName = name;
            Sections = new List<PythonAppConfigSection>(ini.Select(x => new PythonAppConfigSection(appDirectory, x, values?[x.Key])));
            IsSingleSection = Sections.Count == 1 && IsSectionNameUseless(Sections[0].DisplayName, appDirectory);

            foreach (var value in Sections.SelectMany(x => x)) {
                value.PropertyChanged += OnValuePropertyChanged;
            }
        }

        private static bool IsSectionNameUseless(string sectionName, string appDirectory) {
            var appId = Path.GetFileName(appDirectory) ?? "";
            if (sectionName.ToLowerInvariant().ComputeLevenshteinDistance(appId.ToLowerInvariant()) < 3
                    || sectionName.ToLowerInvariant().ComputeLevenshteinDistance("settings") < 3
                    || sectionName.ToLowerInvariant().ComputeLevenshteinDistance("options") < 3
                    || sectionName.ToLowerInvariant().ComputeLevenshteinDistance("parameters") < 3) return true;
            return false;
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
            foreach (var section in Sections) {
                foreach (var value in section) {
                    value.Reset();
                }
            }
        }, () => IsResettable));

        private bool _changed;

        public bool Changed {
            get => _changed;
            set => Apply(value, ref _changed);
        }

        private void OnValuePropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(PythonAppConfigValue.Value)) {
                Changed = true;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Save() {
            Changed = false;
            foreach (var section in Sections) {
                var iniSection = _valuesIniFile[section.Key];
                foreach (var p in section) {
                    iniSection.Set(p.OriginalKey, p.Value);
                }
            }

            _valuesIniFile.Save(true);
        }

        internal bool IsAffectedBy(string changed) {
            return FileUtils.Affects(changed, Filename) || _defaultsFilename != null && FileUtils.Affects(changed, _defaultsFilename);
        }

        [NotNull]
        public List<PythonAppConfigSection> Sections { get; }

        [CanBeNull, ContractAnnotation(@"force:true => notnull")]
        public static PythonAppConfig Create(string filename, string pythonAppLocation, bool force) {
            var relative = FileUtils.GetRelativePath(filename, pythonAppLocation);
            if (!force && !Regex.IsMatch(relative, @"^(?:(?:cfg|config|params|options|settings)[/\\])?[\w-]+\.ini$", RegexOptions.IgnoreCase)) {
                return null;
            }

            const string extension = @".ini";
            const string defaultsPostfix = @"_defaults" + extension;

            if (relative.EndsWith(defaultsPostfix, StringComparison.OrdinalIgnoreCase)) {
                var original = filename.ApartFromLast(defaultsPostfix) + extension;
                if (File.Exists(original)) return null;
                filename = original;
            }

            var defaults = filename.ApartFromLast(extension, StringComparison.OrdinalIgnoreCase) + defaultsPostfix;
            var defaultsMode = File.Exists(defaults);
            var ini = defaultsMode ? new IniFile(defaults, IniFileMode.Comments) : new IniFile(filename, IniFileMode.Comments);
            if (!force && (!ini.Any() || ini.Any(x => !Regex.IsMatch(x.Key, @"^[\w -]+$")))) return null;
            return new PythonAppConfig(filename, pythonAppLocation, ini,
                    relative.ApartFromLast(extension, StringComparison.OrdinalIgnoreCase).Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(AcStringValues.NameFromId).JoinToString('/'),
                    defaultsMode ? new IniFile(filename) : null);
        }

        internal static string CapitalizeFirst(string s) {
            if (s == string.Empty) return string.Empty;
            if (s.Length == 1) return s.ToUpperInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string CapitalizeFirstOnly(string s) {
            if (s == string.Empty) return string.Empty;
            if (s.Length == 1) return s.ToUpperInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
        }

        internal static string ConvertKeyToName(string original) {
            return CapitalizeFirstOnly(Regex.Replace(original, @"[\s_-]+|(?<=[a-z])(?=[A-Z])", " ").Trim());
        }
    }
}