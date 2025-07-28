using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
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

    public sealed class PythonAppConfig : Displayable, IWithId, IDisposable {
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

        private bool _isVisible = true;

        public bool IsVisible {
            get => _isVisible;
            set => Apply(value, ref _isVisible);
        }

        private readonly bool _hasDynamicSections;
        
        public IniFile ValuesIniFileAccess => _valuesIniFile;

        private PythonAppConfig([NotNull] PythonAppConfigParams configParams, string filename, IniFile ini,
                [CanBeNull] Func<string, string, bool> sectionFilter, string name, PythonAppObject parent,
                IniFile values = null, IniFileSection infoOverride = null) {
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
            _sectionsOwn = new List<PythonAppConfigSection>(ini
                    .Select(x => {
                        if (x.Key == "ℹ") {
                            return new PythonAppConfigSection(filename, configParams, infoOverride != null
                                    ? new KeyValuePair<string, IniFileSection>(x.Key, infoOverride) : x, null);
                        }
                        if (sectionFilter?.Invoke(x.Key, x.Value.Commentary) == false) {
                            return null;
                        }
                        return new PythonAppConfigSection(filename, configParams, x, values?[x.Key]);
                    })
                    .NonNull()
                    .Where(x => x.DisplayName != @"hidden"));
            _hasDynamicSections = _sectionsOwn.Any(x => x.PluginSettings != null || x.IsHiddenTest != null);
            IsSingleSection = !_hasDynamicSections && _sectionsOwn.Count == 1
                    && IsSectionNameUseless(_sectionsOwn[0].DisplayName, configParams.PythonAppLocation);
            if (IsSingleSection) {
                _sectionsOwn[0].IsSingleSection = true;
            }
            HasAnythingNew = _sectionsOwn.Any(x => x.Any(y => y.IsNew));

            foreach (var value in _sectionsOwn.SelectMany(x => x)) {
                value.PropertyChanged += OnValuePropertyChanged;
            }

            if (IsResettable) {
                var isNonDefault = false;
                foreach (var section in _sectionsOwn) {
                    if (section.Key == @"ℹ") continue;
                    foreach (var p in section) {
                        if (p.IsNonDefault) {
                            isNonDefault = true;
                        }
                    }
                }
                IsNonDefault = isNonDefault;
            }

            SectionsDisplay = new BetterObservableCollection<PythonAppConfigSection>(_sectionsOwn.Where(x => x.PluginSettings == null));

            var infoSection = _sectionsOwn.GetByIdOrDefault("ℹ");
            if (infoSection != null) {
                _visRequire = infoSection.GetByIdOrDefault("REQUIRE")?.Value;
                if (_visRequire != null) {
                    PatchHelper.FeaturesInvalidated += OnPatchFeaturesChanged;
                }
                _devConfig = infoSection.GetByIdOrDefault("DEVELOPER")?.Value.As(false) == true;
                if (_devConfig) {
                    SettingsHolder.Common.PropertyChanged += OnSettingsPropertyChanged;
                }
                UpdateVisibility();
            }
        }

        private void OnPatchFeaturesChanged(object sender, EventArgs args) {
            UpdateVisibility();
        }

        private string _visRequire;
        private bool _devConfig;

        private void UpdateVisibility() {
            IsVisible = (_visRequire == null || PatchHelper.TestQuery(_visRequire))
                    && (!_devConfig || SettingsHolder.Common.DeveloperMode);
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(SettingsHolder.Common.DeveloperMode)) {
                UpdateVisibility();
            }
        }

        public void Dispose() {
            if (_visRequire != null) {
                PatchHelper.FeaturesInvalidated -= OnPatchFeaturesChanged;
            }
            if (_devConfig) {
                SettingsHolder.Common.PropertyChanged -= OnSettingsPropertyChanged;
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

        private void ResetValues() {
            foreach (var section in _sectionsOwn) {
                foreach (var value in section) {
                    value.Reset();
                }
            }
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
            ResetValues();
            if (_activePluginConfigs.Count == 0) return;
            ActionExtension.InvokeInMainThreadAsync(() => {
                foreach (var config in _activePluginConfigs) {
                    config.Value?.Item2?.ResetValues();
                }
            });
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
                    foreach (var section in _sectionsOwn) {
                        if (section.Key == @"ℹ") continue;
                        foreach (var p in section) {
                            if (p.IsNonDefault) {
                                isNonDefault = true;
                            }
                        }
                    }
                    IsNonDefault = isNonDefault;
                }

                if (sender is PythonAppConfigValue value) {
                    ValueChanged?.Invoke(this, new ValueChangedEventArgs {
                        Source = this,
                        Section = _sectionsOwn.FirstOrDefault(x => x.Contains(value))?.Id,
                        Key = value.OriginalKey,
                        Value = value.Value
                    });

                    if (value.OriginalKey == @"ENABLED") {
                        OnPropertyChanged(nameof(IsActive));
                    }
                }
            }
        }

        private Dictionary<string, Tuple<string, PythonAppConfig>> _activePluginConfigs = new Dictionary<string, Tuple<string, PythonAppConfig>>();

        [CanBeNull]
        private PythonAppConfig GetPluginConfig(string key, [NotNull] string pluginLocation, string userSettingsPath) {
            var item = _activePluginConfigs.GetValueOrSet(key, () => null);
            if (item?.Item1 != pluginLocation) {
                var userSettingsFilename = Path.Combine(AcPaths.GetDocumentsCfgDirectory(),
                        string.Format(userSettingsPath, Path.GetFileName(pluginLocation)));
                var ret = new PythonAppConfigs(new PythonAppConfigParams(pluginLocation) {
                    FilesRelativeDirectory = AcRootDirectory.Instance.RequireValue,
                    ScanFunc = d => Directory.GetFiles(d, "settings.ini"),
                    ConfigFactory = (p, f) => {
                        try {
                            return new[] { PythonAppConfig.Create(p, f, true, userSettingsFilename) };
                        } catch (Exception e) {
                            Logging.Warning(e);
                            return null;
                        }
                    },
                    SaveOnlyNonDefault = true,
                }).FirstOrDefault();
                if (ret != null) {
                    ret.ValueChanged += (sender, args) => ret.Save();
                }
                item = new Tuple<string, PythonAppConfig>(pluginLocation, ret);
                _activePluginConfigs[key] = item;
            }
            return item.Item2;
        }

        private void UpdateSectionsDisplay(PythonAppConfigProvider provider) {
            var insertIndex = 0;
            foreach (var s in _sectionsOwn) {
                if (s.IsHiddenTest != null) {
                    var hidden = s.IsHiddenTest(provider);
                    var existing = SectionsDisplay.IndexOf(s);
                    if (existing == -1 != hidden) {
                        if (hidden) SectionsDisplay.RemoveAt(existing);
                        else SectionsDisplay.Insert(insertIndex++, s);
                    } else if (!hidden) {
                        ++insertIndex;
                    }
                } else if (s.PluginSettings?.Count >= 2) {
                    var reference = provider.GetItem(s.PluginSettings[0]);

                    IReadOnlyList<PythonAppConfigSection> sections = null;
                    if (reference is PythonAppConfigPluginValue v && !string.IsNullOrWhiteSpace(v.Value) && v.IsEnabled) {
                        if (v.SelectedPlugin != null) {
                            sections = GetPluginConfig(s.PluginSettings[0], v.SelectedPlugin.Location, s.PluginSettings[1])?.SectionsOwn;
                        }
                    }

                    var existing = SectionsDisplay.Where(x => x.Tag == s).ToList();
                    if (sections?.SequenceEqual(existing) != true) {
                        foreach (var i in existing) {
                            SectionsDisplay.Remove(i);
                        }
                        if (sections != null) {
                            foreach (var item in sections) {
                                SectionsDisplay.Insert(insertIndex++, item);
                                item.Tag = s;
                            }
                        } else {
                            _activePluginConfigs.Remove(s.PluginSettings[0]);
                        }
                    } else {
                        insertIndex += existing.Count;
                    }
                } else {
                    ++insertIndex;
                }
            }
        }

        public void UpdateReferenced() {
            var provider = new PythonAppConfigProvider(this);
            for (var j = _sectionsOwn.Count - 1; j >= 0; j--) {
                var section = _sectionsOwn[j];
                provider.SetSection(section);

                for (var k = section.Count - 1; k >= 0; k--) {
                    section[k].UpdateReferenced(provider);
                }
            }

            if (_hasDynamicSections) {
                UpdateSectionsDisplay(provider);
            }
        }

        public void ApplyChangesFromIni() {
            Changed = true;

            var hasPlugins = false;
            foreach (var section in _sectionsOwn) {
                if (section.Key == @"ℹ") continue;
                var iniSection = _valuesIniFile[section.Key];
                foreach (var p in section) {
                    if (iniSection.ContainsKey(p.Id)) {
                        p.Value = iniSection.GetPossiblyEmpty(p.Id);
                    } else {
                        p.Reset();
                    }
                }
                hasPlugins = hasPlugins || section.PluginSettings?.Count >= 2;
            }

            if (!hasPlugins) return;
            PythonAppConfigProvider provider = null;
            foreach (var s in _sectionsOwn) {
                if (s.PluginSettings?.Count >= 2) {
                    if (provider == null) {
                        provider = new PythonAppConfigProvider(this);
                    }
                    if (provider.GetItem(s.PluginSettings[0]) is PythonAppConfigPluginValue v
                            && !string.IsNullOrWhiteSpace(v.Value) && v.IsEnabled && v.SelectedPlugin != null) {
                        var value = _valuesIniFile["__PLUGINS"].GetNonEmpty(s.Id)?.FromCutBase64();
                        if (value != null) {
                            GetPluginConfig(s.PluginSettings[0], v.SelectedPlugin.Location, s.PluginSettings[1])?.Import(Encoding.UTF8.GetString(value));
                        }
                    }
                }
            }
        }

        private void ApplyChangesToIni() {
            foreach (var section in _sectionsOwn) {
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

        public void Import(string data) {
            _valuesIniFile.ParseAndReplace(data);
            ApplyChangesFromIni();
            Changed = false;
        }

        public IniFile Export() {
            if (Changed) {
                ApplyChangesToIni();
            }
            IniFile cloned = null;
            PythonAppConfigProvider provider = null;
            foreach (var s in _sectionsOwn) {
                if (s.PluginSettings?.Count >= 2) {
                    if (provider == null) {
                        provider = new PythonAppConfigProvider(this);
                    }
                    if (provider.GetItem(s.PluginSettings[0]) is PythonAppConfigPluginValue v
                            && !string.IsNullOrWhiteSpace(v.Value) && v.IsEnabled && v.SelectedPlugin != null) {
                        if (cloned == null) {
                            cloned = _valuesIniFile.Clone();
                        }
                        cloned["__PLUGINS"].Set(s.Id,
                                GetPluginConfig(s.PluginSettings[0], v.SelectedPlugin.Location, s.PluginSettings[1])?.Export().ToString().ToCutBase64());
                    }
                }
            }
            return cloned ?? _valuesIniFile;
        }

        public string Serialize() {
            return Export().ToString().Replace("\r", "").Trim();
        }

        public void Save() {
            if (!Changed) return;
            Changed = false;
            ApplyChangesToIni();
            _valuesIniFile.Save();
        }

        internal bool IsAffectedBy(string changed) {
            return FileUtils.IsAffectedBy(Filename, changed)
                    || _defaultsFilename != null && FileUtils.IsAffectedBy(_defaultsFilename, changed);
        }

        private readonly List<PythonAppConfigSection> _sectionsOwn;

        public IReadOnlyList<PythonAppConfigSection> SectionsOwn => _sectionsOwn;

        [NotNull]
        public BetterObservableCollection<PythonAppConfigSection> SectionsDisplay { get; }

        [CanBeNull, ContractAnnotation(@"force:true => notnull")]
        public static PythonAppConfig Create([NotNull] PythonAppConfigParams configParams, string valuesFilename, bool force, string userEditedFile = null,
                Func<string, string, bool> sectionFilter = null, IniFile valuesIniOverride = null, IniFileSection infoOverride = null) {
            var relative = FileUtils.GetRelativePath(valuesFilename, configParams.PythonAppLocation);
            if (!force && !Regex.IsMatch(relative, @"^(?:(?:cfg|config|params|options|settings)[/\\])?[\w-]+\.ini$", RegexOptions.IgnoreCase)) {
                return null;
            }

            try {
                const string extension = @".ini";
                string defaults;

                if (userEditedFile == null) {
                    const string defaultsPostfix = @"_defaults" + extension;

                    if (relative.EndsWith(defaultsPostfix, StringComparison.OrdinalIgnoreCase)) {
                        var original = valuesFilename.ApartFromLast(defaultsPostfix) + extension;
                        if (File.Exists(original)) return null;
                        valuesFilename = original;
                    }
                    defaults = valuesFilename.ApartFromLast(extension, StringComparison.OrdinalIgnoreCase) + defaultsPostfix;
                } else {
                    defaults = valuesFilename;
                    valuesFilename = userEditedFile;
                }

                var defaultsMode = File.Exists(defaults);
                var ini = defaultsMode ? new IniFile(defaults, IniFileMode.Comments) : new IniFile(valuesFilename, IniFileMode.Comments);
                if (!force && (!ini.Any() || ini.Any(x => !Regex.IsMatch(x.Key, @"^[\w -]+$")))) {
                    return null;
                }
                return new PythonAppConfig(configParams, valuesFilename, ini, sectionFilter,
                        (ini.ContainsKey("ℹ") ? (infoOverride ?? ini["ℹ"]).GetNonEmpty("FULLNAME") : null)
                                ?? relative.ApartFromLast(extension, StringComparison.OrdinalIgnoreCase)
                                        .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(AcStringValues.NameFromId).JoinToString('/'),
                        PythonAppsManager.Instance.FirstOrDefault(x => x.Location == configParams.PythonAppLocation),
                        valuesIniOverride ?? (defaultsMode ? new IniFile(valuesFilename) : null), infoOverride);
            } catch (Exception e) when (!force) {
                Logging.Warning(e);
                return null;
            }
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
        public string Order => _sectionsOwn.GetByIdOrDefault("ℹ")?.Order;

        [CanBeNull]
        public string Description => _sectionsOwn.GetByIdOrDefault("ℹ")?.Description;

        [CanBeNull]
        public string ShortDescription => _sectionsOwn.GetByIdOrDefault("ℹ")?.ShortDescription;

        public bool IsActive => _sectionsOwn.GetByIdOrDefault("BASIC")?.GetByIdOrDefault("ENABLED")?.Value.As<bool?>() != false;

        public string PresetId => Path.GetFileNameWithoutExtension(Id).ToUpperInvariant();
    }
}