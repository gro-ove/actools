using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppObject : AcCommonObject {
        public PythonAppObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {}

        protected override void LoadOrThrow() {
            Name = Id;
        }

        public override bool HasData => true;

        public override void Save() {
            _lastSaved = DateTime.Now;

            foreach (var config in _configs.Where(x => x.Changed)) {
                config.Save();
            }

            if (Name != Id) {
                FileAcManager.Rename(Id, Name, Enabled);
            }

            UpdateChanged();
        }

        private PythonAppConfigs _configs;
        private DateTime _lastSaved;

        public PythonAppConfigs GetAppConfigs() {
            if (_configs == null) {
                _configs = new PythonAppConfigs(Location, () => {
                    // We’re going to keep it in-memory for now

                    /*if (_configs == null) return;

                    foreach (var config in _configs) {
                        config.PropertyChanged -= OnConfigPropertyChanged;
                    }

                    _configs = null;*/
                });

                foreach (var config in _configs) {
                    config.PropertyChanged += OnConfigPropertyChanged;
                }
            }

            return _configs;
        }

        private void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_configs != null && e.PropertyName == nameof(PythonAppConfig.Changed) && ((PythonAppConfig)sender).Changed) {
                UpdateChanged();
            }
        }

        private void UpdateChanged() {
            Changed = _configs.Any(x => x.Changed) || Name != Id;
        }

        public override bool HandleChangedFile(string filename) {
            if (_configs != null && (DateTime.Now - _lastSaved).TotalSeconds > 3d) {
                for (var i = _configs.Count - 1; i >= 0; i--) {
                    var config = _configs[i];
                    if (config.IsAffectedBy(filename)) {
                        var changed = _configs[i].Changed;
                        _configs[i].PropertyChanged -= OnConfigPropertyChanged;
                        _configs[i] = PythonAppConfig.Create(config.Filename, Location, true);
                        _configs[i].PropertyChanged += OnConfigPropertyChanged;
                        if (changed) {
                            UpdateChanged();
                        }
                    }
                }
            }

            return base.HandleChangedFile(filename);
        }
    }

    public class PythonAppConfigs : BetterObservableCollection<PythonAppConfig>, IDisposable {
        private readonly Action _disposalAction;

        public PythonAppConfigs(string location, Action disposalAction) : base(Directory.GetFiles(location, "*.ini", SearchOption.AllDirectories)
                                                                                        .Select(file => PythonAppConfig.Create(file, location, false))
                                                                                        .Where(cfg => cfg != null)) {
            _disposalAction = disposalAction;
        }

        public void Dispose() {
            _disposalAction?.Invoke();
        }
    }

    public sealed class PythonAppConfig : Displayable {
        internal string Filename { get; }

        private readonly string _defaultsFilename;
        private readonly IniFile _valuesIniFile;

        private PythonAppConfig(string filename, IniFile ini, string name, IniFile values = null) {
            _valuesIniFile = values ?? ini;

            Filename = filename;
            _defaultsFilename = ini.SourceFilename;
            if (_defaultsFilename == Filename) {
                _defaultsFilename = null;
            }

            DisplayName = name;
            Sections = new BetterObservableCollection<PythonAppConfigSection>(ini.Select(x => new PythonAppConfigSection(x, values?[x.Key])));

            foreach (var value in Sections.SelectMany(x => x)) {
                value.PropertyChanged += OnValuePropertyChanged;
            }
        }

        private bool _changed;

        public bool Changed {
            get { return _changed; }
            set {
                if (Equals(value, _changed)) return;
                _changed = value;
                OnPropertyChanged();
            }
        }

        private void OnValuePropertyChanged(object sender, PropertyChangedEventArgs e) {
            Changed = true;
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
            return FileUtils.IsAffected(changed, Filename) || _defaultsFilename != null && FileUtils.IsAffected(changed, _defaultsFilename);
        }

        public BetterObservableCollection<PythonAppConfigSection> Sections { get; }

        [CanBeNull, ContractAnnotation(@"force:true => notnull")]
        public static PythonAppConfig Create(string filename, string pythonAppLocation, bool force) {
            var relative = FileUtils.GetRelativePath(filename, pythonAppLocation);
            if (!force && !Regex.IsMatch(relative, @"^(?:(?:cfg|config|params|options|settings)[/\\])?[\w-]+(?<!_defaults)\.ini$", RegexOptions.IgnoreCase)) {
                return null;
            }

            var defaults = filename.ApartFromLast(@".ini", StringComparison.OrdinalIgnoreCase) + @"_defaults.ini";
            var defaultsMode = File.Exists(defaults);
            var ini = defaultsMode ? new IniFile(defaults, IniFileMode.Comments) : new IniFile(filename, IniFileMode.Comments);
            if (!force && (!ini.Any() || ini.Any(x => !Regex.IsMatch(x.Key, @"^[\w -]+$")))) return null;
            return new PythonAppConfig(filename, ini, relative, defaultsMode ? new IniFile(filename) : null);
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

    public class PythonAppConfigSection : BetterObservableCollection<PythonAppConfigValue> {
        public string Key { get; }

        public string DisplayName { get; }

        public PythonAppConfigSection(KeyValuePair<string, IniFileSection> pair, [CanBeNull] IniFileSection values)
                : base(
                        pair.Value.Select(
                                x => PythonAppConfigValue.Create(x, pair.Value.Commentaries?.GetValueOrDefault(x.Key), values?.GetValueOrDefault(x.Key)))) {
            Key = pair.Key;

            var commentary = pair.Value.Commentary;
            DisplayName = commentary?.Trim() ?? PythonAppConfig.ConvertKeyToName(pair.Key);

            for (var i = Count - 1; i >= 0; i--) {
                var value = this[i];
                if (value.ParentKey != null) {
                    value.SetParent(this.GetByIdOrDefault(value.ParentKey));
                }
            }
        }
    }

    public class PythonAppConfigValue : Displayable, IWithId {
        public string OriginalKey { get; private set; }

        string IWithId.Id => OriginalKey;

        public string ParentKey { get; private set; }

        [CanBeNull]
        public string ToolTip { get; private set; }

        public sealed override string DisplayName { get; set; }

        private string _value;

        public string Value {
            get { return _value; }
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        private bool _enabledInverse;
        private bool _isEnabled = true;

        public bool IsEnabled {
            get { return _isEnabled; }
            set {
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private static readonly Regex ValueCommentaryRegex = new Regex(@"^([^(;]*)(?:\(([^)]+)\))?(?:;(.*))?", RegexOptions.Compiled);

        private static readonly Regex RangeRegex = new Regex(@"^from\s+(\d+(?:\.\d*)?)( ?\S+)?\s+to\s+(\d+(?:\.\d*)?)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex NumberRegex = new Regex(@"^(?:number|float)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BooleanRegex = new Regex(@"^[""`'“”]?([\w-]+)[""`'“”]?\s+or\s+[""`'“”]?([\w-]+)[""`'“”]?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionsRegex = new Regex(@"(?:^|\s*,)\s*(?:[""`'“”](.+?)[""`'“”]|([^,]+))",
                RegexOptions.Compiled);

        private static readonly Regex OptionValueRegex = new Regex(@"^(.+)(?:\s+is\s+|=)(.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DependentRegex = new Regex(@"^(?:(.+);)?\s*(?:(not available)|(only)) with [""`'“”]?([\w-]+)[""`'“”]?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DisabledRegex = new Regex(@"^(?:0|off|disabled|false|no|none)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FileRegex = new Regex(@"^(?:(dir|directory|folder|path)|(?:file|filename)(?:\s+\((.+)\))?$)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected PythonAppConfigValue() {}

        private void Set(string key, string value, [NotNull] string name, [CanBeNull] string toolTip, Tuple<string, bool> dependentKeyDisabled) {
            OriginalKey = key;
            DisplayName = name;
            ToolTip = toolTip ?? key;
            Value = value;

            ParentKey = dependentKeyDisabled?.Item1;
            _enabledInverse = dependentKeyDisabled?.Item2 ?? false;
        }

        [CanBeNull]
        private PythonAppConfigValue _parent;

        internal void SetParent(PythonAppConfigValue value) {
            if (_parent != null) {
                _parent.PropertyChanged -= OnParentPropertyChanged;
            }

            _parent = value;
            UpdateIsEnabled();

            if (_parent != null) {
                _parent.PropertyChanged += OnParentPropertyChanged;
            }
        }

        private void UpdateIsEnabled() {
            if (_parent == null) {
                IsEnabled = true;
                return;
            }

            var b = _parent as PythonAppConfigBoolValue;
            if (b != null) {
                IsEnabled = b.Value ^ _enabledInverse;
            }

            IsEnabled = !DisabledRegex.IsMatch(_parent.Value) ^ _enabledInverse;
        }

        private void OnParentPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Value)) {
                UpdateIsEnabled();
            }
        }

        public static PythonAppConfigValue Create(KeyValuePair<string, string> pair, [CanBeNull] string commentary, [CanBeNull] string actualValue) {
            string name = null, toolTip = null;
            Tuple<string, bool> dependentKeyDisabled = null;
            var result = CreateInner(pair, commentary, ref name, ref toolTip, ref dependentKeyDisabled);

            if (string.IsNullOrEmpty(name)) {
                name = PythonAppConfig.ConvertKeyToName(pair.Key);
            }

            result.Set(pair.Key, actualValue ?? pair.Value, name, toolTip, dependentKeyDisabled);
            return result;
        }

        private static PythonAppConfigValue CreateInner(KeyValuePair<string, string> pair, [CanBeNull] string commentary, [CanBeNull] ref string name,
                [CanBeNull] ref string toolTip, [CanBeNull] ref Tuple<string, bool> dependentKeyDisabled) {
            var value = pair.Value;
            if (commentary != null) {
                var match = ValueCommentaryRegex.Match(commentary);
                if (match.Success) {
                    name = PythonAppConfig.CapitalizeFirst(match.Groups[1].Value.TrimEnd());
                    toolTip = match.Groups[2].Success ? PythonAppConfig.CapitalizeFirst(match.Groups[2].Value.Replace(@"; ", ";\n")) : null;

                    if (name.Length > 50 && toolTip == null) {
                        toolTip = name;
                        name = null;
                    }

                    if (match.Groups[3].Success) {
                        var description = match.Groups[3].Value.Trim();

                        var dependent = DependentRegex.Match(description);
                        if (dependent.Success) {
                            description = dependent.Groups[1].Value;
                            var parent = dependent.Groups[4].Value;
                            var disabledWhen = dependent.Groups[2].Success;
                            dependentKeyDisabled = new Tuple<string, bool>(parent, disabledWhen);
                        }

                        if (NumberRegex.IsMatch(description)) {
                            return new PythonAppConfigNumberValue();
                        }

                        var boolean = BooleanRegex.Match(description);
                        if (boolean.Success) {
                            return new PythonAppConfigBoolValue(boolean.Groups[1].Value, boolean.Groups[2].Value);
                        }

                        var range = RangeRegex.Match(description);
                        if (range.Success) {
                            return new PythonAppConfigRangeValue(FlexibleParser.TryParseDouble(range.Groups[1].Value) ?? 0d,
                                    FlexibleParser.TryParseDouble(range.Groups[3].Value) ?? 100d, range.Groups[2].Success ? range.Groups[2].Value : null);
                        }

                        var file = FileRegex.Match(description);
                        if (file.Success) {
                            return new PythonAppConfigFileValue(file.Groups[1].Success, file.Groups[2].Success ? file.Groups[2].Value : null);
                        }

                        if (description.IndexOf(',') != -1) {
                            var options = OptionsRegex.Matches(description).Cast<Match>()
                                                      .Select(x => (x.Groups[1].Success ? x.Groups[1].Value : x.Groups[2].Value).Trim()).ToArray();
                            if (options.Length > 0) {
                                return new PythonAppConfigOptionsValue(options.Select(x => {
                                    var m = OptionValueRegex.Match(x);
                                    return m.Success ? new SettingEntry(m.Groups[2].Value.TrimStart(), m.Groups[1].Value.TrimEnd()) : new SettingEntry(x, x);
                                }).ToArray());
                            }
                        }

                        // var typeDescription
                    }
                }
            }

            switch (value) {
                case "True":
                case "False":
                    return new PythonAppConfigBoolValue();

                case "true":
                case "false":
                    return new PythonAppConfigBoolValue("true", "false");

                case "on":
                case "off":
                    return new PythonAppConfigBoolValue("on", "off");

                case "TRUE":
                case "FALSE":
                    return new PythonAppConfigBoolValue("TRUE", "FALSE");
            }

            return new PythonAppConfigValue();
        }
    }

    public class PythonAppConfigBoolValue : PythonAppConfigValue {
        private readonly string _trueValue;
        private readonly string _falseValue;

        public new bool Value {
            get { return base.Value == _trueValue; }
            set {
                if (Equals(value, Value)) return;
                base.Value = value ? _trueValue : _falseValue;
            }
        }

        public PythonAppConfigBoolValue([Localizable(false)] string trueValue = "True", [Localizable(false)] string falseValue = "False") {
            _trueValue = trueValue;
            _falseValue = falseValue;
        }
    }

    public class PythonAppConfigFileValue : PythonAppConfigValue {
        public bool DirectoryMode { get; }

        [CanBeNull]
        public string Filter { get; }

        public PythonAppConfigFileValue(bool directoryMode, [CanBeNull] string filter) {
            DirectoryMode = directoryMode;
            Filter = filter?.IndexOf('|') == -1 ? filter + @"|" + filter : filter;
        }
    }

    public class PythonAppConfigNumberValue : PythonAppConfigValue {
        public new double Value {
            get { return FlexibleParser.TryParseDouble(base.Value) ?? 0d; }
            set {
                if (Equals(value, Value)) return;
                base.Value = value.ToInvariantString();
            }
        }
    }

    public class PythonAppConfigOptionsValue : PythonAppConfigValue {
        public IReadOnlyList<SettingEntry> Values { get; }

        public new SettingEntry Value {
            get { return Values.GetByIdOrDefault(base.Value) ?? Values.FirstOrDefault(); }
            set { base.Value = value.Value; }
        }

        public PythonAppConfigOptionsValue(IReadOnlyList<SettingEntry> values) {
            Values = values;
        }
    }

    public class PythonAppConfigRangeValue : PythonAppConfigValue {
        public double Minimum { get; }

        public double Maximum { get; }

        public double Tick { get; }

        public double RoundTo { get; }

        [CanBeNull]
        public string Postfix { get; }

        public new double Value {
            get { return FlexibleParser.TryParseDouble(base.Value) ?? (Minimum + Maximum) / 2; }
            set {
                value = value.Clamp(Minimum, Maximum).Round(RoundTo);
                if (Equals(value, Value)) return;
                base.Value = value.ToInvariantString();
            }
        }

        public PythonAppConfigRangeValue(double minimum, double maximum, [CanBeNull] string postfix) {
            Minimum = minimum;
            Maximum = maximum;
            Postfix = postfix;
            Tick = (maximum - minimum) / 10d;
            RoundTo = Math.Min(Math.Pow(10, Math.Round(Math.Log10(Maximum - Minimum) - 2)), 1d);
        }
    }
}