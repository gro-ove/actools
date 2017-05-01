using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarSetupPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
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

        public class ViewModel : SelectedAcObjectViewModel<CarSetupObject> {
            public CarObject Car { get; }

            [NotNull]
            public ChangeableObservableCollection<SetupEntry> Entries { get; }

            public SetupEntriesTab[] Tabs { get; }

            public SettingEntry[] Tyres { get; }

            private SettingEntry _selectedTyres;

            public SettingEntry SelectedTyres {
                get { return _selectedTyres; }
                set {
                    if (!Tyres.Contains(value)) value = Tyres[0];
                    if (Equals(value, _selectedTyres)) return;
                    _selectedTyres = value;
                    OnPropertyChanged();
                    UpdateMaxSpeed();

                    if (_loading) return;
                    try {
                        _loading = true;
                        SelectedObject.Tyres = value?.IntValue ?? 0;
                    } finally {
                        _loading = false;
                    }
                }
            }

            public ViewModel(CarObject car, [NotNull] CarSetupObject acObject, [CanBeNull] ViewModel previous) : base(acObject) {
                Car = car;

                if (previous != null) {
                    Entries = new ChangeableObservableCollection<SetupEntry>(previous.Entries);
                    Tabs = previous.Tabs;
                } else {
                    var localeProvider = new AcLocaleProvider(AcRootDirectory.Instance.RequireValue, SettingsHolder.Locale.LocaleName, "setup");
                    Entries = new ChangeableObservableCollection<SetupEntry>(car
                            .AcdData?.GetIniFile("setup.ini")
                            .Where(x => x.Value.ContainsKey("TAB") || x.Value.ContainsKey("RATIOS"))
                            .Select(x => new SetupEntry(x.Key, x.Value, localeProvider, car.AcdData))
                            .Where(x => x.Step != 0d)); // TODO!
                    Tabs = Entries?
                            .GroupBy(x => x.TabKey)
                            .Select(x => new SetupEntriesTab(x.Key ?? "?", x.ToArray(), localeProvider))
                            .OrderBy(x => x.DisplayName)
                            .ToArray();
                }
                
                _hasGearEntries = Entries.Any(x => x.Key.StartsWith("GEAR_"));
                LoadValues();
                Entries.ItemPropertyChanged += OnEntryChanged;

                var tyres = Car.AcdData?.GetIniFile("tyres.ini");
                Tyres = tyres?.GetSections("FRONT", -1).Select((x, i) => new SettingEntry(i, x.GetPossiblyEmpty("NAME"))).ToArray();
                SelectedTyres = Tyres?.ElementAtOrDefault(SelectedObject.Tyres);

                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(acObject, nameof(PropertyChanged), Handler);
            }

            public override void Unload() {
                Entries.ItemPropertyChanged -= OnEntryChanged;
                Entries.Clear();
                base.Unload();
            }

            private readonly bool _hasGearEntries;

            private void UpdateMaxSpeed() {
                if (!_hasGearEntries) return;

                var data = Car.AcdData;
                if (data == null) return;

                var finalGearRatio = Entries.FirstOrDefault(x => x.Key == "FINAL_GEAR_RATIO")?.ValuePair.Value ??
                        data.GetIniFile("drivetrain.ini")["GEARS"].GetDoubleNullable("FINAL");
                var limiter = data.GetIniFile("engine.ini")["ENGINE_DATA"].GetDoubleNullable("LIMITER");
                var tyreRadius = data.GetIniFile("tyres.ini").GetSections("REAR", -1).Skip(SelectedTyres.IntValue ?? 0)
                                     .FirstOrDefault()?.GetDoubleNullable("RADIUS");
                var tyreLength = 2 * Math.PI * tyreRadius;
                foreach (var entry in Entries.Where(x => x.Key.StartsWith("GEAR_"))) {
                    entry.RatioMaxSpeed = limiter / entry.ValuePair.Value / finalGearRatio * tyreLength / 1e3 * 60;
                }
            }

            private bool _loading;

            private void LoadValues() {
                if (_loading) return;
                try {
                    _loading = true;
                    foreach (var entry in Entries) {
                        entry.LoadValue(SelectedObject.GetValue(SetupKeyToSavedKey(entry.Key)));
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
                    SelectedObject.SetValue(SetupKeyToSavedKey(entry.Key), entry.Value);

                    if (entry.Key.StartsWith("GEAR_") || entry.Key == "FINAL_GEAR_RATIO") {
                        UpdateMaxSpeed();
                    }
                } finally {
                    _loading = false;
                }
            }

            private CommandBase _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                SelectedObject.Track = SelectTrackDialog.Show(SelectedObject.Track)?.MainTrackObject;
            }));

            private CommandBase _clearTrackCommand;

            public ICommand ClearTrackCommand => _clearTrackCommand ?? (_clearTrackCommand = new DelegateCommand(() => {
                SelectedObject.TrackId = null;
                _clearTrackCommand?.RaiseCanExecuteChanged();
            }, () => SelectedObject.TrackId != null));

            private void Handler(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(SelectedObject.Tyres):
                        if (!_loading) {
                            try {
                                _loading = true;
                                SelectedTyres = Tyres.ElementAtOrDefault(SelectedObject.Tyres);
                                UpdateMaxSpeed();
                            } finally {
                                _loading = false;
                            }
                        }
                        break;
                    case nameof(SelectedObject.Values):
                        LoadValues();
                        UpdateMaxSpeed();
                        break;
                }
            }

            private CommandBase _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(() => {
                var data = SharingHelper.SetMetadata(SharedEntryType.CarSetup, FileUtils.ReadAllText(SelectedObject.Location),
                        new SharedMetadata {
                            [@"car"] = Car.Id,
                            [@"track"] = SelectedObject.TrackId
                        });
                var target = SelectedObject.Track == null ? Car.Name : $"{Car.Name} ({SelectedObject.Track.Name})";
                return SharingUiHelper.ShareAsync(SharedEntryType.CarSetup, SelectedObject.Name, target, data);
            }));

            private CommandBase _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new AsyncCommand(() => {
                var setupId = SelectedObject.Id.ApartFromLast(SelectedObject.Extension).Replace('\\', '/');
                return QuickDrive.RunAsync(Car, track: SelectedObject.Track, carSetupId: setupId);
            }));

            protected override string PrepareIdForInput(string id) {
                if (string.IsNullOrWhiteSpace(id)) return null;
                return Path.GetFileName(id.ApartFromLast(SelectedObject.Extension, StringComparison.OrdinalIgnoreCase));
            }

            protected override string FixIdFromInput(string id) {
                if (string.IsNullOrWhiteSpace(id)) return null;
                return Path.Combine(Path.GetDirectoryName(SelectedObject.Id) ?? CarSetupObject.GenericDirectory,
                        id + SelectedObject.Extension);
            }
        }

        private string _carId, _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _carId = uri.GetQueryParam("CarId");
            if (_carId == null) throw new ArgumentException(ToolsStrings.Common_CarIdIsMissing);

            _id = uri.GetQueryParam("Id");
            if (_id == null) throw new ArgumentException(ToolsStrings.Common_IdIsMissing);
        }

        private CarObject _carObject;
        private CarSetupObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            do {
                _carObject = await CarsManager.Instance.GetByIdAsync(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                await Task.Run(() => {
                    _carObject.AcdData?.GetIniFile("car.ini");
                    _carObject.AcdData?.GetIniFile("setup.ini");
                    _carObject.AcdData?.GetIniFile("tyres.ini");
                }, cancellationToken);

                _object = await _carObject.SetupsManager.GetByIdAsync(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Load() {
            do {
                _carObject = CarsManager.Instance.GetById(_carId);
                if (_carObject == null) {
                    _object = null;
                    return;
                }

                _object = _carObject?.SetupsManager.GetById(_id);
            } while (_carObject.Outdated);
        }

        void ILoadableContent.Initialize() {
            if (_carObject == null) throw new ArgumentException(AppStrings.Common_CannotFindCarById);
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);
            
            SetModel();
            InitializeComponent();
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_carObject, _object, _model));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
            });
        }

        public bool ImmediateChange(Uri uri) {
            var carId = uri.GetQueryParam("CarId");
            if (carId == null || carId != _carId) return false;

            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = _carObject?.SetupsManager.GetById(id);
            if (obj == null) return false;

            _object = obj;
            SetModel();
            return true;
        }

        private ViewModel _model;
    }
}
