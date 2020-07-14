using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ValueChangedEventArgs : EventArgs {
        public PythonAppConfig Source { get; set; }
        public string Section { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public sealed class PythonAppConfig : Displayable, IWithId {
        public PythonAppObject Parent { get; }

        [NotNull]
        public PythonAppConfigParams ConfigParams { get; }

        public string Filename { get; }
        public string FileNameWithoutExtension { get; }

        private readonly string _defaultsFilename;
        private readonly IniFile _valuesIniFile;

        public event EventHandler<ValueChangedEventArgs> ValueChanged;

        public bool IsResettable { get; }
        public bool IsSingleSection { get; }

        private bool _isNonDefault;

        public bool IsNonDefault {
            get => _isNonDefault;
            set => Apply(value, ref _isNonDefault);
        }

        private bool _hasAnythingNew;

        public bool HasAnythingNew {
            get => _hasAnythingNew;
            set => Apply(value, ref _hasAnythingNew);
        }

        private PythonAppConfig([NotNull] PythonAppConfigParams configParams, string filename, IniFile ini, string name, PythonAppObject parent,
                IniFile values = null) {
            IsResettable = values != null;
            _valuesIniFile = values ?? ini;

            ConfigParams = configParams;
            Filename = filename;
            FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename)?.ToLowerInvariant();
            Parent = parent;

            _defaultsFilename = ini.Filename;
            if (_defaultsFilename == Filename) {
                _defaultsFilename = null;
            }

            DisplayName = name.Trim();
            Sections = new List<PythonAppConfigSection>(ini.Select(x =>
                    new PythonAppConfigSection(configParams, x, values?[x.Key]))
                    .Where(x => x.DisplayName != @"hidden"));
            IsSingleSection = Sections.Count == 1 && IsSectionNameUseless(Sections[0].DisplayName, configParams.PythonAppLocation);
            HasAnythingNew = Sections.Any(x => x.Any(y => y.IsNew));

            foreach (var value in Sections.SelectMany(x => x)) {
                value.PropertyChanged += OnValuePropertyChanged;
            }

            if (IsResettable) {
                var isNonDefault = false;
                foreach (var section in Sections) {
                    if (section.Key == @"ℹ") continue;
                    foreach (var p in section) {
                        if (p.IsNonDefault) {
                            isNonDefault = true;
                        }
                    }
                }
                IsNonDefault = isNonDefault;
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

        public static bool IgnoreChanges = false;

        private void OnValuePropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (!IgnoreChanges && e.PropertyName == nameof(PythonAppConfigValue.Value)) {
                Changed = true;

                if (IsResettable) {
                    var isNonDefault = false;
                    foreach (var section in Sections) {
                        if (section.Key == @"ℹ") continue;
                        foreach (var p in section) {
                            if (p.IsNonDefault) {
                                isNonDefault = true;
                            }
                        }
                    }
                    IsNonDefault = isNonDefault;
                }

                if (sender is PythonAppConfigValue value)
                {
                    ValueChanged?.Invoke(this, new ValueChangedEventArgs {
                        Source = this,
                        Section = Sections.FirstOrDefault(x => x.Contains(value))?.Id,
                        Key = value.OriginalKey,
                        Value = value.Value
                    });

                    if (value.OriginalKey == @"ENABLED") {
                        OnPropertyChanged(nameof(IsActive));
                    }
                }
            }
        }

        public void UpdateReferenced() {
            var provider = new PythonAppConfigProvider(this);
            for (var j = Sections.Count - 1; j >= 0; j--) {
                var section = Sections[j];
                provider.SetSection(section);

                for (var k = section.Count - 1; k >= 0; k--) {
                    section[k].UpdateReferenced(provider);
                }
            }
        }

        public void ApplyChangesFromIni() {
            Changed = true;
            foreach (var section in Sections) {
                if (section.Key == @"ℹ") continue;
                var iniSection = _valuesIniFile[section.Key];
                foreach (var p in section) {
                    if (iniSection.ContainsKey(p.Id)) {
                        p.Value = iniSection.GetPossiblyEmpty(p.Id);
                    } else {
                        p.Reset();
                    }
                }
            }
        }

        private void ApplyChangesToIni() {
            foreach (var section in Sections) {
                if (section.Key == @"ℹ") continue;
                var iniSection = _valuesIniFile[section.Key];
                foreach (var p in section) {
                    if (ConfigParams.SaveOnlyNonDefault && !p.IsNonDefault) {
                        iniSection.Remove(p.Id);
                    } else {
                        iniSection.Set(p.Id, p.Value);
                    }
                }
            }
        }

        public IniFile ValuesIni() {
            Changed = false;
            return _valuesIniFile;
        }

        public IniFile Export() {
            if (Changed) {
                ApplyChangesToIni();
            }
            return _valuesIniFile;
        }

        public void Save() {
            if (!Changed) return;
            Changed = false;
            ApplyChangesToIni();
            _valuesIniFile.Save();
        }

        internal bool IsAffectedBy(string changed) {
            return FileUtils.IsAffectedBy(Filename, changed) || _defaultsFilename != null && FileUtils.IsAffectedBy(_defaultsFilename, changed);
        }

        [NotNull]
        public List<PythonAppConfigSection> Sections { get; }

        [CanBeNull, ContractAnnotation(@"force:true => notnull")]
        public static PythonAppConfig Create([NotNull] PythonAppConfigParams configParams, string filename, bool force, string userEditedFile = null) {
            var relative = FileUtils.GetRelativePath(filename, configParams.PythonAppLocation);
            if (!force && !Regex.IsMatch(relative, @"^(?:(?:cfg|config|params|options|settings)[/\\])?[\w-]+\.ini$", RegexOptions.IgnoreCase)) {
                return null;
            }

            const string extension = @".ini";
            string defaults;

            if (userEditedFile == null) {
                const string defaultsPostfix = @"_defaults" + extension;

                if (relative.EndsWith(defaultsPostfix, StringComparison.OrdinalIgnoreCase)) {
                    var original = filename.ApartFromLast(defaultsPostfix) + extension;
                    if (File.Exists(original)) return null;
                    filename = original;
                }
                defaults = filename.ApartFromLast(extension, StringComparison.OrdinalIgnoreCase) + defaultsPostfix;
            } else {
                defaults = filename;
                filename = userEditedFile;
            }

            var defaultsMode = File.Exists(defaults);
            var ini = defaultsMode ? new IniFile(defaults, IniFileMode.Comments) : new IniFile(filename, IniFileMode.Comments);
            if (!force && (!ini.Any() || ini.Any(x => !Regex.IsMatch(x.Key, @"^[\w -]+$")))) return null;
            return new PythonAppConfig(configParams, filename, ini,
                    (ini.ContainsKey("ℹ") ? ini["ℹ"].GetNonEmpty("FULLNAME") : null)
                            ?? relative.ApartFromLast(extension, StringComparison.OrdinalIgnoreCase)
                                    .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(AcStringValues.NameFromId).JoinToString('/'),
                    PythonAppsManager.Instance.FirstOrDefault(x => x.Location == configParams.PythonAppLocation),
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

        [NotNull]
        public string Id => Filename;

        [CanBeNull]
        public string Order => Sections.GetByIdOrDefault("ℹ")?.Order;

        [CanBeNull]
        public string Description => Sections.GetByIdOrDefault("ℹ")?.Description;

        [CanBeNull]
        public string ShortDescription => Sections.GetByIdOrDefault("ℹ")?.ShortDescription;

        public bool IsActive => Sections.GetByIdOrDefault("BASIC")?.GetByIdOrDefault("ENABLED")?.Value.As<bool?>() != false;

        public string PresetId => Path.GetFileNameWithoutExtension(Id).ToUpperInvariant();
    }
}