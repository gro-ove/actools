using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public class CarSetupValues : NotifyPropertyChanged, IDisposable {
        private static string SavedKeyToSetupKey(string savedKey) {
            if (savedKey.StartsWith("INTERNAL_GEAR_")) {
                var gear = savedKey.Substring("INTERNAL_GEAR_".Length).AsInt();
                return $"GEAR_{gear - 1}";
            }

            return savedKey;
        }

        private static string SetupKeyToSavedKey(string setupKey) {
            if (setupKey.StartsWith("GEAR_")) {
                var gear = setupKey.Substring("GEAR_".Length).AsInt();
                return $"INTERNAL_GEAR_{gear + 1}";
            }

            return setupKey;
        }

        private static string FixName(string name, bool titleCase) {
            if (string.IsNullOrWhiteSpace(name)) return "?";

            var s = Regex.Replace(name, @"[^a-zA-Z0-9\.,-]+|(?<=[a-z])(?=[A-Z])", " ").Trim().Split(' ');
            var b = new List<string>();
            foreach (var p in s) {
                if (p.Length < 3 && !Regex.IsMatch(p, @"^(?:a|an|as|at|by|en|if|in|of|on|or|the|to|vs)$") ||
                        Regex.IsMatch(p, @"^(?:arb)$")) {
                    b.Add(p.ToUpperInvariant());
                } else {
                    b.Add(titleCase || b.Count == 0 ? char.ToUpper(p[0]) + p.Substring(1).ToLowerInvariant() :
                            p.ToLowerInvariant());
                }
            }
            return b.JoinToString(' ');
        }

        public enum StepsMode {
            ActualValue = 0, StepsNormalized = 1, Steps = 2
        }

        public sealed class SetupEntry : Displayable {
            public string Key { get; }

            public string TabKey { get; }

            public string HelpInformation { get; }

            public double Minimum { get; }

            public double Maximum { get; }

            public double Step { get; }

            public StepsMode StepsMode { get;}

            public string UnitsPostfix { get; }

            public static string GetUnitsPostfix(string key) {
                switch (key) {
                    case "DIFF_POWER":
                    case "DIFF_COAST":
                    case "FRONT_DIFF_COAST":
                    case "FRONT_DIFF_POWER":
                    case "REAR_DIFF_COAST":
                    case "REAR_DIFF_POWER":
                    case "CENTER_DIFF_PRELOAD":
                    case "AWD_FRONT_TORQUE_DISTRIBUTION":
                    case "ENGINE_LIMITER":
                    case "FRONT_BIAS":
                    case "BRAKE_POWER_MULT":
                        return "%";
                }

                switch (key.Split('_')[0]) {
                    case "PRESSURE":
                        return ControlsStrings.Common_PsiPostfix;
                    case "FUEL":
                        return " L";
                    case "TOE":
                    case "CAMBER":
                    case "WING":
                    case "PACKER":
                        return " mm";
                    default:
                        return null;
                }
            }

            private double? GetDefaultValue(string key, DataWrapper data) {
                switch (key) {
                    case "DIFF_POWER":
                        return data.GetIniFile("drivetrain.ini")["DIFFERENTIAL"].GetDouble("POWER", 0.5) * 100d;
                    case "DIFF_COAST":
                        return data.GetIniFile("drivetrain.ini")["DIFFERENTIAL"].GetDouble("COAST", 0.5) * 100d;
                    case "FINAL_GEAR_RATIO":
                        return data.GetIniFile("drivetrain.ini")["GEARS"].GetDouble("FINAL", 3.0);
                    case "FUEL":
                        return data.GetIniFile("car.ini")["FUEL"].GetDouble("FUEL", 20);
                    case "PRESSURE_LF":
                    case "PRESSURE_RF":
                        return data.GetIniFile("tyres.ini")["FRONT"].GetDouble("PRESSURE_STATIC", 20);
                    case "PRESSURE_LR":
                    case "PRESSURE_RR":
                        return data.GetIniFile("tyres.ini")["REAR"].GetDouble("PRESSURE_STATIC", 20);
                }

                /*if (key.StartsWith("INTERNAL_GEAR_")) {
                    var gearKey = SavedKeyToSetupKey(key);
                    var value = gearKey == null ? null : data.GetIniFile("drivetrain.ini")["GEARS"].GetDoubleNullable(gearKey);
                    if (value == null) return null;

                    return Values.FindIndex(x => x.Value == value);
                }*/

                switch (key.Split('_')[0]) {
                    case "GEAR":
                        var value = data.GetIniFile("drivetrain.ini")["GEARS"].GetDoubleNullable(key);
                        return value == null ? (double?)null : Values.FindIndex(x => x.Value == value);

                    default:
                        return null;
                }
            }

            private static double? FixedStep(string key) {
                switch (key.Split('_')[0]) {
                    case "CAMBER":
                        return 0.1d;
                    default:
                        return null;
                }
            }

            public SetupEntry(string key, IniFileSection section, [NotNull] AcLocaleProvider localeProvider, [NotNull] DataWrapper data) {
                Key = key;
                DisplayName = localeProvider.GetString("SETUP", section.GetNonEmpty("NAME")) ?? FixName(section.GetNonEmpty("NAME"), false);
                HelpInformation = localeProvider.GetString(AcLocaleProvider.CategoryTag, section.GetNonEmpty("HELP")) ?? section.GetNonEmpty("HELP");

                var ratios = section.GetNonEmpty("RATIOS");
                if (ratios != null) {
                    Values = data.GetRtoFile(ratios).Values;
                    Minimum = 0;
                    Maximum = Values.Count - 1;
                    Step = 1;
                    StepsMode = StepsMode.Steps;
                    TabKey = "GEARS";
                } else {
                    Minimum = section.GetDouble("MIN", 0);
                    Maximum = section.GetDouble("MAX", Minimum + 100);
                    Step = FixedStep(key) ?? section.GetDouble("STEP", 1d);
                    StepsMode = section.GetIntEnum("SHOW_CLICKS", StepsMode.ActualValue);
                    UnitsPostfix = StepsMode == StepsMode.ActualValue ? GetUnitsPostfix(key) : null;
                    TabKey = section.GetNonEmpty("TAB");
                }

                var defaultValue = GetDefaultValue(key, data);
                DefaultValue = defaultValue ?? (Minimum + Maximum) / 2f;
                HasDefaultValue = defaultValue.HasValue;

                var range = Maximum - Minimum;
                VisualStep = range / Step < 10 ? Step : range / 10;
            }

            private double? _value;

            public double Value {
                get { return _value ?? 0d; }
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                    if (Values != null) {
                        ValuePair = Values.ElementAtOrDefault(value.RoundToInt());
                    }
                }
            }

            private KeyValuePair<string, double> _valuePair;

            public KeyValuePair<string, double> ValuePair {
                get { return _valuePair; }
                set {
                    if (Equals(value, _valuePair)) return;
                    _valuePair = value;
                    OnPropertyChanged();
                    Value = Values.IndexOf(value);
                }
            }

            public Dictionary<string, double> Values { get; }

            public void LoadValue(double? saved) {
                if (!saved.HasValue) {
                    Value = DefaultValue;
                } else {
                    switch (StepsMode) {
                        case StepsMode.ActualValue:
                            Value = saved.Value * (FixedStep(Key) ?? 1d);
                            break;
                        case StepsMode.StepsNormalized:
                            Value = saved.Value * Step;
                            break;
                        case StepsMode.Steps:
                            Value = saved.Value * Step + Minimum;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            public double VisualStep { get; }

            public double DefaultValue { get; }

            public bool HasDefaultValue { get; }

            private double? _ratioMaxSpeed;

            public double? RatioMaxSpeed {
                get { return _ratioMaxSpeed; }
                set {
                    if (Equals(value, _ratioMaxSpeed)) return;
                    _ratioMaxSpeed = value;
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _resetCommand;

            public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
                Value = DefaultValue;
            }));
        }

        public sealed class SetupEntriesTab : Displayable {
            public SetupEntriesTab(string key, SetupEntry[] entries, AcLocaleProvider localeProvider) {
                Key = key;
                DisplayName = FixName(localeProvider.GetString("TABS", key) ?? key, true);
                Entries = entries;
            }

            public string Key { get; }

            public SetupEntry[] Entries { get; }
        }

        [NotNull]
        private readonly ChangeableObservableCollection<SetupEntry> _entries;
        public SetupEntriesTab[] Tabs { get; }
        public SettingEntry[] Tyres { get; }
        private SettingEntry _selectedTyres;

        [CanBeNull]
        public SettingEntry SelectedTyres {
            get => _selectedTyres;
            set {
                if (!Tyres.Contains(value)) value = Tyres[0];
                if (Equals(value, _selectedTyres)) return;
                _selectedTyres = value;
                OnPropertyChanged();
                UpdateMaxSpeed();

                if (_loading || !(_selectedObject is CarSetupObject)) return;
                try {
                    _loading = true;
                    ((CarSetupObject)_selectedObject).Tyres = value?.IntValue ?? 0;
                } finally {
                    _loading = false;
                }
            }
        }

        private readonly CarObject _car;
        private readonly ICarSetupObject _selectedObject;
        private readonly bool _hasGearEntries;

        public CarSetupValues(CarObject car, ICarSetupObject selectedObject, CarSetupValues previous) {
            _car = car;
            _selectedObject = selectedObject;

            if (previous?._entries != null) {
                _entries = previous._entries;
                Tabs = previous.Tabs;
            } else {
                var localeProvider = new AcLocaleProvider(AcRootDirectory.Instance.RequireValue, SettingsHolder.Locale.LocaleName, "setup");
                _entries = new ChangeableObservableCollection<SetupEntry>(car
                        .AcdData?.GetIniFile("setup.ini")
                        .Where(x => x.Value.ContainsKey("TAB") || x.Value.ContainsKey("RATIOS"))
                        .Select(x => new SetupEntry(x.Key, x.Value, localeProvider, car.AcdData))
                        .Where(x => x.Step != 0d)); // TODO!
                Tabs = _entries?
                        .GroupBy(x => x.TabKey)
                        .Select(x => new SetupEntriesTab(x.Key ?? "?", x.ToArray(), localeProvider))
                        .OrderBy(x => x.DisplayName)
                        .ToArray();
            }

            _hasGearEntries = _entries.Any(x => x.Key.StartsWith("GEAR_"));

            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            Tyres = tyres?.GetSections("FRONT", -1).Select((x, i) => new SettingEntry(i, x.GetPossiblyEmpty("NAME"))).ToArray() ?? new SettingEntry[0];

            _entries.ItemPropertyChanged += OnEntryChanged;
            EnsureLoaded().Forget();
        }

        private bool _isLoaded;

        public bool IsLoaded {
            get { return _isLoaded; }
            set {
                if (Equals(value, _isLoaded)) return;
                _isLoaded = value;
                OnPropertyChanged();
            }
        }

        private async Task EnsureLoaded() {
            await _selectedObject.EnsureDataLoaded();
            IsLoaded = true;
            LoadValues();
            SelectedTyres = Tyres?.ElementAtOrDefault(_selectedObject.Tyres ?? -1);
            _selectedObject.SubscribeWeak(Handler);
        }

        private bool _loading;

        private void LoadValues() {
            if (_loading) return;
            try {
                _loading = true;
                foreach (var entry in _entries) {
                    entry.LoadValue(_selectedObject.GetValue(SetupKeyToSavedKey(entry.Key)));
                }
            } finally {
                _loading = false;
            }
        }

        private void OnEntryChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SetupEntry.Value) || _loading) return;
            try {
                _loading = true;
                var entry = (SetupEntry)sender;
                _selectedObject.SetValue(SetupKeyToSavedKey(entry.Key), entry.Value);

                if (entry.Key.StartsWith("GEAR_") || entry.Key == "FINAL_GEAR_RATIO") {
                    UpdateMaxSpeed();
                }
            } finally {
                _loading = false;
            }
        }

        private void UpdateMaxSpeed() {
            if (!_hasGearEntries) return;

            var data = _car.AcdData;
            if (data == null) return;

            var finalGearRatio = _entries.FirstOrDefault(x => x.Key == "FINAL_GEAR_RATIO")?.ValuePair.Value ??
                    data.GetIniFile("drivetrain.ini")["GEARS"].GetDoubleNullable("FINAL");
            var limiter = data.GetIniFile("engine.ini")["ENGINE_DATA"].GetDoubleNullable("LIMITER");
            var tyreRadius = data.GetIniFile("tyres.ini").GetSections("REAR", -1).Skip(SelectedTyres?.IntValue ?? 0)
                                 .FirstOrDefault()?.GetDoubleNullable("RADIUS");
            var tyreLength = 2 * Math.PI * tyreRadius;
            foreach (var entry in _entries.Where(x => x.Key.StartsWith("GEAR_"))) {
                entry.RatioMaxSpeed = limiter / entry.ValuePair.Value / finalGearRatio * tyreLength / 1e3 * 60;
            }
        }

        private void Handler(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(_selectedObject.Tyres):
                    if (!_loading) {
                        try {
                            _loading = true;
                            SelectedTyres = Tyres.ElementAtOrDefault(_selectedObject.Tyres ?? -1);
                            UpdateMaxSpeed();
                        } finally {
                            _loading = false;
                        }
                    }
                    break;
                case nameof(_selectedObject.Values):
                    LoadValues();
                    UpdateMaxSpeed();
                    break;
            }
        }

        public void Dispose() {
            _entries.ItemPropertyChanged -= OnEntryChanged;
        }
    }
}