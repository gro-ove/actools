using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Objects {
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
                OnValueChanged();
            }
        }

        protected virtual void OnValueChanged() { }

        private bool _isEnabled = true;

        public bool IsEnabled {
            get => _isEnabled;
            set => Apply(value, ref _isEnabled);
        }

        public string AppDirectory { get; private set; }

        private static readonly Regex ValueCommentaryRegex = new Regex(@"^([^(;]*)(?:\(([^)]+)\))?(?:;(.*))?", RegexOptions.Compiled);

        private static readonly Regex RangeRegex = new Regex(@"^from\s+(\d+(?:\.\d*)?)( ?\S+)?\s+to\s+(\d+(?:\.\d*)?)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex NumberRegex = new Regex(@"^(?:number|float)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex KeyRegex = new Regex(@"^(?:key|button|keyboard|keyboard button)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BooleanRegex = new Regex(@"^([\w-]+)\s+or\s+([\w-]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionsRegex = new Regex(@"(?:^|\s*,|\s*\bor\b)\s*(?:[""`'“”](.+?)[""`'“”]|(((?!\bor\b)[^,])+))",
                RegexOptions.Compiled);

        private static readonly Regex OptionValueRegex = new Regex(@"^(.+)(?:\s+is\s+|=)(.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OptionValueAltRegex = new Regex(@"^(.+)(?:\s+for\s+|=)(.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DependentRegex = new Regex(@"^(?:(.+);)?\s*(?:(not available)|(only)) with (.+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DisabledRegex = new Regex(@"^(?:0|off|disabled|false|no|none)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FileRegex = new Regex(@"^(local\s+)?(?:(dir|directory|folder|path)|(?:file|filename)(?:\s+\((.+)\))?$)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal class CustomBooleanTestEntry : ITestEntry {
            private readonly bool _value;

            public CustomBooleanTestEntry(bool b) {
                _value = b;
            }

            public void Set(ITestEntryFactory factory) {}

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
            set => Apply(value, ref _isResettable);
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(Reset, () => _originalValue != null));

        public static PythonAppConfigValue Create(string appDirectory, KeyValuePair<string, string> pair, [CanBeNull] string commentary,
                [CanBeNull] string actualValue, bool isResetable) {
            string name = null, toolTip = null;
            Func<IPythonAppConfigValueProvider, bool> isEnabledTest = null;
            var result = CreateInner(pair, commentary, ref name, ref toolTip, ref isEnabledTest);
            result.AppDirectory = appDirectory;

            if (string.IsNullOrEmpty(name)) {
                name = PythonAppConfig.ConvertKeyToName(pair.Key);
            }

            result.Set(pair.Key, actualValue ?? pair.Value, name, toolTip, isEnabledTest, isResetable ? pair.Value : null);
            return result;
        }

        private class TesterInner : ITester<IPythonAppConfigValueProvider> {
            private readonly Func<string, string> _unwrap;

            public TesterInner(Func<string, string> unwrap) {
                _unwrap = unwrap;
            }

            public string ParameterFromKey(string key) {
                return key;
            }

            public bool Test(IPythonAppConfigValueProvider obj, string key, ITestEntry value) {
                return key == null || value.Test(obj.Get(_unwrap(key)));
            }
        }

        public static Func<IPythonAppConfigValueProvider, bool> CreateDisabledFunc(string query, bool invert, Func<string, string> unwrap) {
            query = query
                    .Replace(" and ", " & ")
                    .Replace(" or ", " | ")
                    .Replace(" not ", " ! ");

            var filter = Filter.Create(new TesterInner(unwrap), query, new FilterParams {
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

        [NotNull]
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
                        var description = match.Groups[3].Value.Trim().WrapQuoted(out var unwrap);

                        var dependent = DependentRegex.Match(description);
                        if (dependent.Success) {
                            description = dependent.Groups[1].Value;
                            isEnabledTest = CreateDisabledFunc(dependent.Groups[4].Value.Trim(), dependent.Groups[2].Success, unwrap);
                        }

                        if (NumberRegex.IsMatch(description)) {
                            return new PythonAppConfigNumberValue();
                        }

                        if (KeyRegex.IsMatch(description)) {
                            return new PythonAppConfigKeyValue();
                        }

                        var range = RangeRegex.Match(description);
                        if (range.Success) {
                            return new PythonAppConfigRangeValue(FlexibleParser.TryParseDouble(range.Groups[1].Value) ?? 0d,
                                    FlexibleParser.TryParseDouble(range.Groups[3].Value) ?? 100d, range.Groups[2].Success ? unwrap(range.Groups[2].Value) : null);
                        }

                        var file = FileRegex.Match(description);
                        if (file.Success) {
                            return new PythonAppConfigFileValue(file.Groups[2].Success, !file.Groups[1].Success, file.Groups[3].Success ? unwrap(file.Groups[3].Value) : null);
                        }

                        if (description.IndexOf(',') != -1) {
                            var options = OptionsRegex.Matches(description).Cast<Match>()
                                                      .Select(x => (x.Groups[1].Success ? x.Groups[1].Value : x.Groups[2].Value).Trim()).ToArray();
                            if (options.Length > 0) {
                                return new PythonAppConfigOptionsValue(options.Select(x => {
                                    var m1 = OptionValueAltRegex.Match(x);
                                    if (m1.Success) {
                                        return new SettingEntry(unwrap(m1.Groups[1].Value.TrimStart()),
                                                PythonAppConfig.CapitalizeFirst(unwrap(m1.Groups[2].Value.TrimEnd())));
                                    }

                                    var m2 = OptionValueRegex.Match(x);
                                    if (m2.Success) {
                                        return new SettingEntry(unwrap(m2.Groups[2].Value.TrimStart()),
                                                PythonAppConfig.CapitalizeFirst(unwrap(m2.Groups[1].Value.TrimEnd())));
                                    }

                                    x = unwrap(x);
                                    return new SettingEntry(x, AcStringValues.NameFromId(x.ToLowerInvariant()));
                                }).ToArray());
                            }
                        }

                        var boolean = BooleanRegex.Match(description);
                        if (boolean.Success) {
                            return new PythonAppConfigBoolValue(unwrap(boolean.Groups[1].Value), unwrap(boolean.Groups[2].Value));
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
}