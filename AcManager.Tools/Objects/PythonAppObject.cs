using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Objects {
    public class PythonAppObject : AcCommonObject, IAcObjectVersionInformation {
        public static readonly string[] VersionSources = { "version.txt", "changelog.txt", "readme.txt", "read me.txt" };
        private static readonly Regex VersionRegex = new Regex(@"^\s{0,4}(?:-\s*)?v?(\d+(?:\.\d+)+)", RegexOptions.Compiled);

        [CanBeNull]
        public static string GetVersion(string fileData) {
            return fileData.Split('\n').Select(x => VersionRegex.Match(x))
                           .Where(x => x.Success).Select(x => x.Groups[1].Value)
                           .Aggregate((string)null, (a, b) => a == null || a.IsVersionOlderThan(b) ? b : a);
        }

        public PythonAppObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) { }

        protected override void LoadOrThrow() {
            Name = Id;
            TryToLoadVersion();
        }

        private void TryToLoadVersion() {
            try {
                foreach (var candidate in VersionSources) {
                    var filename = Path.Combine(Location, candidate);
                    if (File.Exists(filename)) {
                        Version = GetVersion(File.ReadAllText(filename));
                        if (Version != null) break;
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e);
                Version = null;
            }
        }

        public override bool HasData => true;

        public override void Save() {
            _lastSaved = DateTime.Now;

            foreach (var config in _configs.Where(x => x.Changed)) {
                config.Save();
            }

            if (Name != Id) {
                FileAcManager.RenameAsync(Id, Name, Enabled);
            } else {
                UpdateChanged();
            }
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

                _configs.ValueChanged += OnConfigsValueChanged;
            }

            return _configs;
        }

        private void OnConfigsValueChanged(object sender, EventArgs e) {
            if (_configs != null) {
                UpdateChanged();
            } else {
                ((PythonAppConfigs)sender).ValueChanged -= OnConfigsValueChanged;
            }
        }

        private void UpdateChanged() {
            Changed = _configs.Any(x => x.Changed) || Name != Id;
        }

        public override bool HandleChangedFile(string filename) {
            if (_configs != null && (DateTime.Now - _lastSaved).TotalSeconds > 3d && _configs.HandleChanged(Location, filename)) {
                UpdateChanged();
                return true;
            }

            if (VersionSources.Contains(FileUtils.GetRelativePath(filename, Location).ToLowerInvariant())) {
                TryToLoadVersion();
                return true;
            }

            return base.HandleChangedFile(filename);
        }

        private string _version;

        public string Version {
            get => _version;
            private set {
                if (value == _version) return;
                _version = value;
                OnPropertyChanged();
            }
        }
    }

    public class PythonAppConfigs : ObservableCollection<PythonAppConfig>, IDisposable {
        public event EventHandler ValueChanged;

        private readonly Action _disposalAction;

        private static IEnumerable<string> GetSubConfigFiles(string directory) {
            var inis = Directory.GetFiles(directory, "*.ini");
            return inis.Length > 10 ? new string[0] : inis;
        }

        private static IEnumerable<string> GetConfigFiles(string directory) {
            var inis = Directory.GetFiles(directory, "*.ini");
            return (inis.Length > 10 ? new string[0] : inis).Concat(Directory.GetDirectories(directory).SelectMany(GetSubConfigFiles));
        }

        public PythonAppConfigs(string location, Action disposalAction) : base(GetConfigFiles(location)
                .Select(file => PythonAppConfig.Create(file, location, false)).Where(cfg => cfg != null)) {
            _disposalAction = disposalAction;
            UpdateEnabled();

            foreach (var config in this) {
                config.ValueChanged += OnValueChanged;
            }
        }

        public bool HandleChanged(string location, string filename) {
            var result = false;
            var updated = false;

            for (var i = Count - 1; i >= 0; i--) {
                var config = this[i];
                if (config.IsAffectedBy(filename)) {
                    if (config.Changed) {
                        result = true;
                    }

                    config.ValueChanged -= OnValueChanged;
                    this[i] = PythonAppConfig.Create(config.Filename, location, true);
                    this[i].ValueChanged += OnValueChanged;

                    updated = true;
                }
            }

            if (updated) {
                UpdateEnabled();
            }

            return result;
        }

        private void OnValueChanged(object sender, EventArgs e) {
            UpdateEnabled();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private class ValueProvider : IPythonAppConfigValueProvider {
            private readonly ObservableCollection<PythonAppConfig> _root;
            private List<PythonAppConfigSection> _config;
            private Collection<PythonAppConfigValue> _section;

            public ValueProvider(ObservableCollection<PythonAppConfig> root) {
                _root = root;
            }

            private static int LastIndexOf(string key) {
                int i0 = key.LastIndexOf('/'), i1 = key.LastIndexOf('\\');
                return i0 != -1 ? i1 != -1 ? i0 < i1 ? i1 : i0 : i0 : i1;
            }

            private static int LastIndexOf(string key, int from) {
                int i0 = key.LastIndexOf('/', from), i1 = key.LastIndexOf('\\', from);
                return i0 != -1 ? i1 != -1 ? i0 < i1 ? i1 : i0 : i0 : i1;
            }

            private static void Parse(string key, out string param, out string section, out string file) {
                var paramSep = LastIndexOf(key);
                if (paramSep <= 0) {
                    param = paramSep == -1 ? key : key.Substring(1);
                    section = file = null;
                    return;
                }

                param = key.Substring(paramSep + 1);

                var sectionSep = LastIndexOf(key, paramSep - 1);
                if (sectionSep <= 0) {
                    section = sectionSep == -1 ? key.Substring(0, paramSep) : key.Substring(1, paramSep - 1);
                    file = null;
                    return;
                }

                section = key.Substring(sectionSep + 1, paramSep - sectionSep - 1);
                file = key.Substring(0, sectionSep);
            }

            public string Get(string key) {
                Parse(key, out var param, out var section, out var file);

                var sections = file == null ? _config : _root.FirstOrDefault(x => string.Equals(x.DisplayName, file, StringComparison.OrdinalIgnoreCase))?.Sections;
                if (sections == null) return null;

                var values = section == null ? _section : sections.FirstOrDefault(x => string.Equals(x.Key, section, StringComparison.OrdinalIgnoreCase));
                return values?.FirstOrDefault(x => string.Equals(x.OriginalKey, param))?.Value;
            }

            public void SetConfig(List<PythonAppConfigSection> config) {
                _config = config;
            }

            public void SetSection(Collection<PythonAppConfigValue> section) {
                _section = section;
            }
        }

        public void UpdateEnabled() {
            var provider = new ValueProvider(this);
            for (var i = 0; i < Count; i++) {
                var config = this[i];
                provider.SetConfig(config.Sections);

                for (var j = config.Sections.Count - 1; j >= 0; j--) {
                    var section = config.Sections[j];
                    provider.SetSection(section);

                    for (var k = section.Count - 1; k >= 0; k--) {
                        var value = section[k];
                        if (value.IsEnabledTest != null) {
                            value.IsEnabled = value.IsEnabledTest(provider);
                        }
                    }
                }
            }
        }

        public void Dispose() {
            _disposalAction?.Invoke();
        }
    }

    public sealed class PythonAppConfig : Displayable {
        internal string Filename { get; }

        private readonly string _defaultsFilename;
        private readonly IniFile _valuesIniFile;

        public event EventHandler ValueChanged;

        public bool IsResettable { get; }

        private PythonAppConfig(string filename, IniFile ini, string name, IniFile values = null) {
            IsResettable = values != null;
            _valuesIniFile = values ?? ini;

            Filename = filename;
            _defaultsFilename = ini.Filename;
            if (_defaultsFilename == Filename) {
                _defaultsFilename = null;
            }

            DisplayName = name;
            Sections = new List<PythonAppConfigSection>(ini.Select(x => new PythonAppConfigSection(x, values?[x.Key])));

            foreach (var value in Sections.SelectMany(x => x)) {
                value.PropertyChanged += OnValuePropertyChanged;
            }
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
            set {
                if (Equals(value, _changed)) return;
                _changed = value;
                OnPropertyChanged();
            }
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
            return FileUtils.IsAffected(changed, Filename) || _defaultsFilename != null && FileUtils.IsAffected(changed, _defaultsFilename);
        }

        public List<PythonAppConfigSection> Sections { get; }

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

    public class PythonAppConfigSection : ObservableCollection<PythonAppConfigValue> {
        public string Key { get; }

        public string DisplayName { get; }

        public PythonAppConfigSection(KeyValuePair<string, IniFileSection> pair, [CanBeNull] IniFileSection values)
                : base(pair.Value.Select(x => PythonAppConfigValue.Create(x,
                        pair.Value.Commentaries?.GetValueOrDefault(x.Key), values?.GetValueOrDefault(x.Key), values != null))) {
            Key = pair.Key;

            var commentary = pair.Value.Commentary;
            DisplayName = commentary?.Trim() ?? PythonAppConfig.ConvertKeyToName(pair.Key);
        }
    }

    public interface IPythonAppConfigValueProvider {
        string Get(string key);
    }

    public class PythonAppConfigValue : Displayable, IWithId {
        public string OriginalKey { get; private set; }

        string IWithId<string>.Id => OriginalKey;

        public Func<IPythonAppConfigValueProvider, bool> IsEnabledTest { get; private set; }

        [CanBeNull]
        public string ToolTip { get; private set; }

        public sealed override string DisplayName { get; set; }

        private string _value;

        public string Value {
            get => _value;
            set {
                if (Equals(value, _value)) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        // private bool _enabledInverse;
        private bool _isEnabled = true;

        public bool IsEnabled {
            get => _isEnabled;
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

        private static readonly Regex DependentRegex = new Regex(@"^(?:(.+);)?\s*(?:(not available)|(only)) with (.+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DisabledRegex = new Regex(@"^(?:0|off|disabled|false|no|none)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FileRegex = new Regex(@"^(?:(dir|directory|folder|path)|(?:file|filename)(?:\s+\((.+)\))?$)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal class CustomBooleanTestEntry : ITestEntry {
            private readonly bool _value;

            public CustomBooleanTestEntry(bool b) {
                _value = b;
            }

            public bool Test(bool value) {
                return value == _value;
            }

            public bool Test(double value) {
                return _value != Equals(value, 0.0);
            }

            public bool Test(string value) {
                return Test(value != null && !DisabledRegex.IsMatch(value));
            }

            public bool Test(TimeSpan value) {
                return Test(value > default(TimeSpan));
            }

            public bool Test(DateTime value) {
                return Test(value > default(DateTime));
            }
        }

        protected PythonAppConfigValue() { }

        private string _originalValue;

        private void Set(string key, string value, [NotNull] string name, [CanBeNull] string toolTip, Func<IPythonAppConfigValueProvider, bool> isEnabledTest,
                string originalValue) {
            OriginalKey = key;
            DisplayName = name;
            ToolTip = toolTip ?? key;
            Value = value;
            IsEnabledTest = isEnabledTest;
            _originalValue = originalValue;
            _resetCommand?.RaiseCanExecuteChanged();
            IsResettable = _originalValue != null;
        }

        public void Reset() {
            if (_originalValue == null) return;
            Value = _originalValue;
        }

        private bool _isResettable;

        public bool IsResettable {
            get => _isResettable;
            set {
                if (value == _isResettable) return;
                _isResettable = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(Reset, () => _originalValue != null));

        public static PythonAppConfigValue Create(KeyValuePair<string, string> pair, [CanBeNull] string commentary, [CanBeNull] string actualValue,
                bool isResetable) {
            string name = null, toolTip = null;
            Func<IPythonAppConfigValueProvider, bool> isEnabledTest = null;
            var result = CreateInner(pair, commentary, ref name, ref toolTip, ref isEnabledTest);

            if (string.IsNullOrEmpty(name)) {
                name = PythonAppConfig.ConvertKeyToName(pair.Key);
            }

            result.Set(pair.Key, actualValue ?? pair.Value, name, toolTip, isEnabledTest, isResetable ? pair.Value : null);
            return result;
        }

        private class TesterInner : ITester<IPythonAppConfigValueProvider> {
            public string ParameterFromKey(string key) {
                return key;
            }

            public bool Test(IPythonAppConfigValueProvider obj, string key, ITestEntry value) {
                return key == null || value.Test(obj.Get(key));
            }
        }

        public static Func<IPythonAppConfigValueProvider, bool> CreateDisabledFunc(string query, bool invert) {
            query = query
                    .Replace(" and ", " & ")
                    .Replace(" or ", " | ")
                    .Replace(" not ", " ! ");

            var filter = Filter.Create(new TesterInner(), query, new FilterParams {
                StringMatchMode = StringMatchMode.StartsWith,
                BooleanTestFactory = b => new CustomBooleanTestEntry(b),
                ValueSplitter = new ValueSplitter(ValueSplitFunc.Custom, ValueSplitFunc.Separators),
                ValueConversion = null
            });

            if (invert) return p => !filter.Test(p);
            return filter.Test;
        }

        internal static class ValueSplitFunc {
            private static readonly Regex ParsingRegex = new Regex(@"^(.+?)([:<>≥≤=+-])\s*", RegexOptions.Compiled);
            public static readonly char[] Separators = { ':', '<', '>', '≥', '≤', '=', '+', '-' };

            private static string ClearKey(string key) {
                return key?.Trim().Trim('"', '\'', '`', '“', '”');
            }

            public static FilterPropertyValue Custom(string s) {
                var match = ParsingRegex.Match(s);
                if (!match.Success) return new FilterPropertyValue(ClearKey(s), FilterComparingOperation.IsTrue);

                var key = match.Groups[1].Value;
                var operation = (FilterComparingOperation)match.Groups[2].Value[0];
                var value = s.Substring(match.Length);
                return new FilterPropertyValue(ClearKey(key), operation, ClearKey(value));
            }
        }

        private static PythonAppConfigValue CreateInner(KeyValuePair<string, string> pair, [CanBeNull] string commentary, [CanBeNull] ref string name,
                [CanBeNull] ref string toolTip, [CanBeNull] ref Func<IPythonAppConfigValueProvider, bool> isEnabledTest) {
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
                            isEnabledTest = CreateDisabledFunc(dependent.Groups[4].Value.Trim(), dependent.Groups[2].Success);
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

                case "TRUE":
                case "FALSE":
                    return new PythonAppConfigBoolValue("TRUE", "FALSE");

                case "On":
                case "Off":
                    return new PythonAppConfigBoolValue("On", "Off");

                case "on":
                case "off":
                    return new PythonAppConfigBoolValue("on", "off");

                case "ON":
                case "OFF":
                    return new PythonAppConfigBoolValue("ON", "OFF");
            }

            return new PythonAppConfigValue();
        }
    }

    public class PythonAppConfigBoolValue : PythonAppConfigValue {
        private readonly string _trueValue;
        private readonly string _falseValue;

        public new bool Value {
            get => base.Value == _trueValue;
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
            get => FlexibleParser.TryParseDouble(base.Value) ?? 0d;
            set {
                if (Equals(value, Value)) return;
                base.Value = value.ToInvariantString();
            }
        }
    }

    public class PythonAppConfigOptionsValue : PythonAppConfigValue {
        public IReadOnlyList<SettingEntry> Values { get; }

        public new SettingEntry Value {
            get => Values.GetByIdOrDefault(base.Value) ?? Values.FirstOrDefault();
            set => base.Value = value.Value;
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
            get => FlexibleParser.TryParseDouble(base.Value) ?? (Minimum + Maximum) / 2;
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