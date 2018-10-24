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
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed class PythonAppConfig : Displayable, IWithId {
        [NotNull]
        public PythonAppConfigParams ConfigParams { get; }

        internal string Filename { get; }

        private readonly string _defaultsFilename;
        private readonly IniFile _valuesIniFile;

        public event EventHandler ValueChanged;

        public bool IsResettable { get; }
        public bool IsSingleSection { get; }

        private bool _isNonDefault;

        public bool IsNonDefault {
            get => _isNonDefault;
            set => Apply(value, ref _isNonDefault);
        }

        private PythonAppConfig([NotNull] PythonAppConfigParams configParams, string filename, IniFile ini, string name, IniFile values = null) {
            IsResettable = values != null;
            _valuesIniFile = values ?? ini;

            ConfigParams = configParams;
            Filename = filename;

            _defaultsFilename = ini.Filename;
            if (_defaultsFilename == Filename) {
                _defaultsFilename = null;
            }

            DisplayName = name.Trim();
            Sections = new List<PythonAppConfigSection>(ini.Select(x => new PythonAppConfigSection(configParams, x, values?[x.Key]))
                                                           .Where(x => x.DisplayName != @"hidden"));
            IsSingleSection = Sections.Count == 1 && IsSectionNameUseless(Sections[0].DisplayName, configParams.PythonAppLocation);

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

        private void OnValuePropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(PythonAppConfigValue.Value)) {
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

                ValueChanged?.Invoke(this, EventArgs.Empty);

                if (sender is PythonAppConfigValue value && value.OriginalKey == @"ENABLED") {
                    OnPropertyChanged(nameof(IsActive));
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

        public void Save() {
            if (!Changed) return;
            Changed = false;

            foreach (var section in Sections) {
                if (section.Key == @"ℹ") continue;
                var iniSection = _valuesIniFile[section.Key];
                foreach (var p in section) {
                    // if (ConfigParams.SaveOnlyChanged && !p.IsChanged) continue;
                    if (ConfigParams.SaveOnlyNonDefault && !p.IsNonDefault) {
                        iniSection.Remove(p.OriginalKey);
                    } else {
                        iniSection.Set(p.OriginalKey, p.Value);
                    }
                }
            }

            _valuesIniFile.Save(true);
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

        public string Id => Filename;
        public string Order => Sections.GetByIdOrDefault("ℹ")?.Order;
        public string Description => Sections.GetByIdOrDefault("ℹ")?.Description;
        public string ShortDescription => Sections.GetByIdOrDefault("ℹ")?.ShortDescription;
        public bool IsActive => Sections.GetByIdOrDefault("BASIC")?.GetByIdOrDefault("ENABLED")?.Value.As<bool?>() != false;
    }
}