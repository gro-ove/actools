// ReSharper disable RedundantUsingDirective

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Xml.Schema;
using Windows.UI.Popups;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Tyres;
using AcTools.DataFile;
using AcTools.NeuralTyres;
using AcTools.NeuralTyres.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using OxyPlot;
using SlimDX.DXGI;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class CarCreateTyresMachineDialog {
        public static readonly string NeuralParamsKey = "Tyres Generation";
        public static readonly PresetsCategory NeuralParamsCategory = new PresetsCategory(System.IO.Path.Combine("Tyres Generation", "Neural Params"));

        public static readonly string NeuralMachinesKey = "Tyres Machines";

        public static readonly PresetsCategory NeuralMachinesCategory = new PresetsCategory(System.IO.Path.Combine("Tyres Generation", "Tyres Machines"),
                ".zip");

        public static int OptionSupportedVersion = 10;

        private ViewModel Model => (ViewModel)DataContext;

        public CarCreateTyresMachineDialog([CanBeNull] CarObject carToTest, List<CarWithTyres> list) {
            DataContext = new ViewModel(list);
            InitializeComponent();

            var onTrack = new MenuItem {
                Header = "Start race immediately",
                IsCheckable = true
            };
            onTrack.SetBinding(MenuItem.IsCheckedProperty, new Stored("/CreateTyres.TestOnTrack", true));
            Buttons = new Control[] {
                CreateExtraDialogButton(AppStrings.Toolbar_Save, Model.SaveCommand, true),
                carToTest == null ? null : new ButtonWithComboBox {
                    Content = AppStrings.Common_Test,
                    Command = Model.TestCommand,
                    Margin = new Thickness(4, 0, 0, 0),
                    MinHeight = 21,
                    MinWidth = 65,
                    CommandParameter = carToTest,
                    MenuItems = {
                        onTrack
                    }
                },
                CancelButton
            };

            this.OnActualUnload(Model);

            foreach (var s in Model.Cars.Where(x => x.IsChecked)) {
                CarsListBox.SelectedItems.Add(s);
            }

            CarsListBox.SelectionChanged += OnSelectionChanged;
            CarsListBox.SelectAll();
        }

        public class CarTyres : NotifyPropertyChanged {
            public TyresEntry Entry { get; }

            public CarTyres(TyresEntry entry) {
                Entry = entry;
            }

            private bool _isChecked;

            public bool IsChecked {
                get => _isChecked;
                set {
                    if (Equals(value, _isChecked)) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public class CarWithTyres : NotifyPropertyChanged {
            public CarWithTyres(CarObject car, List<CarTyres> tyres) {
                Car = car;
                Tyres = tyres;
                Tyres.ForEach(x => x.PropertyChanged += OnTyrePropertyChanged);
                UpdateChecked();
            }

            private void OnTyrePropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(CarTyres.IsChecked)) {
                    UpdateChecked();
                }
            }

            public CarObject Car { get; }
            public List<CarTyres> Tyres { get; }

            private bool _isChecked;

            public bool IsChecked {
                get => _isChecked;
                set {
                    if (Equals(value, _isChecked)) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }

            private int _checkedCount;

            public int CheckedCount {
                get => _checkedCount;
                set {
                    if (Equals(value, _checkedCount)) return;
                    _checkedCount = value;
                    OnPropertyChanged();
                }
            }

            private string _displayChecked = ToolsStrings.Common_None;

            public string DisplayChecked {
                get => _displayChecked;
                set {
                    if (string.IsNullOrEmpty(value)) value = ToolsStrings.Common_None;
                    if (Equals(value, _displayChecked)) return;
                    _displayChecked = value;
                    OnPropertyChanged();
                }
            }

            private void UpdateChecked() {
                CheckedCount = Tyres.Count(x => x.IsChecked);
                DisplayChecked = Tyres.Where(x => x.IsChecked).Select(x => x.Entry.DisplayName).JoinToReadableString();
            }
        }

        public static readonly string DefaultNeuralLayers = NeuralTyresOptions.Default.Layers.JoinToString(@"; ");
        public static readonly bool DefaultSeparateNetworks = NeuralTyresOptions.Default.SeparateNetworks;
        public static readonly bool DefaultHighPrecision = NeuralTyresOptions.Default.HighPrecision;
        public static readonly int DefaultTrainingRuns = NeuralTyresOptions.Default.TrainingRuns;
        public static readonly int DefaultAverageAmount = NeuralTyresOptions.Default.AverageAmount;
        public static readonly FannTrainingAlgorithm DefaultFannAlgorithm = NeuralTyresOptions.Default.FannAlgorithm;
        public static readonly double DefaultLearningMomentum = NeuralTyresOptions.Default.LearningMomentum;
        public static readonly double DefaultLearningRate = NeuralTyresOptions.Default.LearningRate;
        public static readonly double DefaultRandomBounds = NeuralTyresOptions.Default.RandomBounds;

        public static SettingEntry[] FannAlgorithms { get; } = EnumExtension.GetValues<FannTrainingAlgorithm>().Select(x => {
            var n = x.GetDescription().Split(new[] { ':' }, 2);
            return new SettingEntry((int)x, n[0]) {
                Tag = n[1]
            };
        }).ToArray();

        private class ViewModel : NotifyPropertyChanged, IUserPresetable, IDisposable, IComparer {
            private class SaveableData {
                public string Layers = DefaultNeuralLayers;
                public bool SeparateNetworks = DefaultSeparateNetworks, HighPrecision = DefaultHighPrecision;
                public int TrainingRuns = DefaultTrainingRuns, AverageAmount = DefaultAverageAmount;
                public FannTrainingAlgorithm FannAlgorithm = DefaultFannAlgorithm;
                public double LearningMomentum = DefaultLearningMomentum, LearningRate = DefaultLearningRate, RandomBounds = DefaultRandomBounds;
            }

            private readonly ISaveHelper _saveable;

            public List<CarWithTyres> Cars { get; }
            public BetterListCollectionView CarsView { get; }

            public ViewModel(List<CarWithTyres> cars) {
                OutputKeys = new BetterObservableCollection<SettingEntry>();
                OutputKeysView = new BetterListCollectionView(OutputKeys) {
                    GroupDescriptions = {
                        new PropertyGroupDescription(nameof(SettingEntry.Tag))
                    },
                    CustomSort = this
                };

                TestKeys = new BetterObservableCollection<SettingEntry>();
                TestKeysView = new BetterListCollectionView(TestKeys) {
                    GroupDescriptions = {
                        new PropertyGroupDescription(nameof(SettingEntry.Tag))
                    },
                    CustomSort = this
                };

                Cars = cars;
                UpdateListFilter();
                UpdateTyresFilter();
                UpdateSummary();

                CarsView = new BetterListCollectionView(Cars) {
                    Filter = o => {
                        var c = (CarWithTyres)o;
                        return (_listFilterInstance?.Test(c.Car) ?? true) && c.Tyres.Any(x => x.IsChecked);
                    }
                };

                Cars.ForEach(x => {
                    x.PropertyChanged += (sender, args) => {
                        if (args.PropertyName == nameof(CarWithTyres.IsChecked)) {
                            UpdateSummary();
                        }
                    };

                    x.Tyres.ForEach(y => {
                        y.PropertyChanged += (sender, args) => {
                            if (args.PropertyName == nameof(CarTyres.IsChecked)) {
                                UpdateSummary();
                            }
                        };
                    });
                });

                (_saveable = new SaveHelper<SaveableData>("_carTextureDialog", () => new SaveableData {
                    Layers = NeuralLayers,
                    SeparateNetworks = NeuralSeparateNetworks,
                    TrainingRuns = NeuralTrainingRuns,
                    AverageAmount = NeuralAverageAmount,
                    LearningMomentum = NeuralLearningMomentum,
                    LearningRate = NeuralLearningRate,
                    HighPrecision = NeuralHighPrecision,
                    RandomBounds = NeuralRandomBounds,
                    FannAlgorithm = (FannTrainingAlgorithm?)NeuralFannAlgorithm.IntValue ?? DefaultFannAlgorithm,
                }, o => {
                    NeuralLayers = o.Layers;
                    NeuralSeparateNetworks = o.SeparateNetworks;
                    NeuralTrainingRuns = o.TrainingRuns;
                    NeuralAverageAmount = o.AverageAmount;
                    NeuralLearningMomentum = o.LearningMomentum;
                    NeuralLearningRate = o.LearningRate;
                    NeuralHighPrecision = o.HighPrecision;
                    NeuralRandomBounds = o.RandomBounds;
                    NeuralFannAlgorithm = FannAlgorithms.GetByIdOrDefault((int?)o.FannAlgorithm) ?? FannAlgorithms.GetById((int?)DefaultFannAlgorithm);
                })).Initialize();
            }

            bool IUserPresetable.CanBeSaved => true;
            string IUserPresetable.PresetableKey => NeuralParamsKey;
            PresetsCategory IUserPresetable.PresetableCategory => NeuralParamsCategory;

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            private void SaveLater() {
                _saveable.SaveLater();
                Changed?.Invoke(this, EventArgs.Empty);
            }

            #region Tyres selection
            private readonly StoredValue<string> _carsFilter = Stored.Get("/CreateTyres.CarsFilter", "kunos+");

            [CanBeNull]
            public string CarsFilter {
                get => _carsFilter.Value;
                set {
                    if (Equals(value, _carsFilter.Value)) return;
                    _carsFilter.Value = value;
                    OnPropertyChanged();
                    UpdateListFilter();
                }
            }

            private IFilter<CarObject> _listFilterInstance;

            private void UpdateListFilter() {
                _listFilterInstance = Filter.Create(CarObjectTester.Instance, CarsFilter ?? "*");
                CarsView?.Refresh();
            }

            private readonly StoredValue<string> _tyresFilter = Stored.Get("/CreateTyres.TyresFilter", "semislicks");

            [CanBeNull]
            public string TyresFilter {
                get => _tyresFilter.Value;
                set {
                    if (Equals(value, _tyresFilter.Value)) return;
                    _tyresFilter.Value = value;
                    OnPropertyChanged();
                    UpdateTyresFilter();
                }
            }

            private IFilter<TyresEntry> _tyresFilterInstance;

            private void UpdateTyresFilter() {
                _tyresFilterInstance = Filter.Create(TyresEntryTester.Instance, TyresFilter ?? "*");

                for (var i = Cars.Count - 1; i >= 0; i--) {
                    var car = Cars[i];
                    for (var j = car.Tyres.Count - 1; j >= 0; j--) {
                        var tyre = car.Tyres[j];
                        tyre.IsChecked = _tyresFilterInstance.Test(tyre.Entry);
                    }
                }

                UpdateSummary();
                CarsView?.Refresh();
            }

            private TyresEntry[] _tyres;
            private readonly Busy _updateSummaryBusy = new Busy();

            private void UpdateSummary() {
                _updateSummaryBusy.Yield(() => {
                    var tyres = Cars.SelectMany(x => x.IsChecked ? x.Tyres.Where(y => y.IsChecked) : new CarTyres[0]).Select(x => x.Entry)
                                    .Distinct(TyresEntry.TyresEntryComparer).ToArray();
                    _tyres = tyres;

                    Radius.Reset();
                    RimRadius.Reset();
                    Width.Reset();

                    for (var i = tyres.Length - 1; i >= 0; i--) {
                        var tyre = tyres[i];
                        Radius.Update(tyre.Radius);
                        RimRadius.Update(tyre.RimRadius);
                        Width.Update(tyre.Width);
                    }

                    TotalTyresCount = Cars.Sum(x => x.IsChecked ? x.CheckedCount : 0);
                    UniqueTyresCount = tyres.Length;
                    Radius.Apply(ValuePadding);
                    RimRadius.Apply(ValuePadding);
                    Width.Apply(ValuePadding);

                    if (_tyres.Length > 0) {
                        var testKey = SelectedTestKey;
                        TestKeys.ReplaceEverythingBy_Direct(_tyres[0].MainSection.Keys.Concat(_tyres[0].ThermalSection.Keys.Select(x => ThermalPrefix + x))
                                                                     .Select(GetOutputKeyEntry));
                        SelectedTestKey = TestKeys.GetByIdOrDefault(testKey.Value) ?? testKey;
                    }

                    // SuggestedMachineName = tyres.GroupBy(x => x.Name).MaxEntryOrDefault(x => x.Count())?.First().Name;
                });
            }

            private int _totalTyresCount;

            public int TotalTyresCount {
                get => _totalTyresCount;
                set {
                    if (Equals(value, _totalTyresCount)) return;
                    _totalTyresCount = value;
                    OnPropertyChanged();
                }
            }

            private int _uniqueTyresCount;

            public int UniqueTyresCount {
                get => _uniqueTyresCount;
                set {
                    if (Equals(value, _uniqueTyresCount)) return;
                    _uniqueTyresCount = value;
                    OnPropertyChanged();
                    _createTyresMachineCommand?.RaiseCanExecuteChanged();
                }
            }

            public DisplayDoubleRange Radius { get; } = new DisplayDoubleRange(100, " cm");
            public DisplayDoubleRange RimRadius { get; } = new DisplayDoubleRange(100, " cm");
            public DisplayDoubleRange Width { get; } = new DisplayDoubleRange(100, " cm");

            private readonly StoredValue<double> _valuePadding = Stored.Get("/CreateTyres.ValuePadding", 0.3);

            public double ValuePadding {
                get => _valuePadding.Value;
                set {
                    if (Equals(value, _valuePadding.Value)) return;
                    _valuePadding.Value = value;
                    OnPropertyChanged();
                    UpdateSummary();
                }
            }
            #endregion

            #region Generation
            private readonly StoredValue<bool> _testSingleKey = Stored.Get("/CreateTyres.TestSingleKey", false);

            public bool TestSingleKey {
                get => _testSingleKey.Value;
                set {
                    if (Equals(value, _testSingleKey.Value)) return;
                    _testSingleKey.Value = value;
                    OnPropertyChanged();
                }
            }

            [NotNull]
            public BetterObservableCollection<SettingEntry> TestKeys { get; }

            public BetterListCollectionView TestKeysView { get; }

            private SettingEntry _selectedTestKey = DefaultOutputKey;

            public SettingEntry SelectedTestKey {
                get => _selectedTestKey;
                set {
                    if (TestKeys.Contains(value) == false) {
                        value = TestKeys.GetByIdOrDefault(_selectedTestKey?.Value) ?? TestKeys.FirstOrDefault() ?? DefaultOutputKey;
                    }
                    if (Equals(value, _selectedTestKey)) return;
                    _selectedTestKey = value;
                    OnPropertyChanged();
                    UpdatePlotModel();
                }
            }

            private SettingEntry _neuralFannAlgorithm = DefaultOutputKey;

            public SettingEntry NeuralFannAlgorithm {
                get => _neuralFannAlgorithm;
                set {
                    if (FannAlgorithms.Contains(value) == false) {
                        value = FannAlgorithms.GetById((int?)DefaultFannAlgorithm);
                    }
                    if (Equals(value, _neuralFannAlgorithm)) return;
                    _neuralFannAlgorithm = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private string _neuralLayers = DefaultNeuralLayers;

            public string NeuralLayers {
                get => _neuralLayers;
                set {
                    if (Equals(value, _neuralLayers)) return;
                    _neuralLayers = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _neuralSeparateNetworks = DefaultSeparateNetworks;

            public bool NeuralSeparateNetworks {
                get => _neuralSeparateNetworks;
                set {
                    if (Equals(value, _neuralSeparateNetworks)) return;
                    _neuralSeparateNetworks = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _neuralHighPrecision = DefaultHighPrecision;

            public bool NeuralHighPrecision {
                get => _neuralHighPrecision;
                set {
                    if (Equals(value, _neuralHighPrecision)) return;
                    _neuralHighPrecision = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private int _neuralTrainingRuns = DefaultTrainingRuns;

            public int NeuralTrainingRuns {
                get => _neuralTrainingRuns;
                set {
                    if (Equals(value, _neuralTrainingRuns)) return;
                    _neuralTrainingRuns = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private int _neuralAverageAmount = DefaultAverageAmount;

            public int NeuralAverageAmount {
                get => _neuralAverageAmount;
                set {
                    if (Equals(value, _neuralAverageAmount)) return;
                    _neuralAverageAmount = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _neuralLearningMomentum = DefaultLearningMomentum;

            public double NeuralLearningMomentum {
                get => _neuralLearningMomentum;
                set {
                    if (Equals(value, _neuralLearningMomentum)) return;
                    _neuralLearningMomentum = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _neuralLearningRate = DefaultLearningRate;

            public double NeuralLearningRate {
                get => _neuralLearningRate;
                set {
                    if (Equals(value, _neuralLearningRate)) return;
                    _neuralLearningRate = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _neuralRandomBounds = DefaultRandomBounds;

            public double NeuralRandomBounds {
                get => _neuralRandomBounds;
                set {
                    if (Equals(value, _neuralRandomBounds)) return;
                    _neuralRandomBounds = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> _createTyresMachineCommand;

            public AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> CreateTyresMachineCommand => _createTyresMachineCommand
                    ?? (_createTyresMachineCommand =
                            new AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>>(CreateTyresMachineAsync, c => UniqueTyresCount > 1));

            private async Task CreateTyresMachineAsync(Tuple<IProgress<AsyncProgressEntry>, CancellationToken> tuple) {
                var progress = tuple?.Item1;
                var cancellation = tuple?.Item2 ?? CancellationToken.None;
                progress?.Report(new AsyncProgressEntry("Initialization…", 0.001d));

                try {
                    GeneratedMachine = null;

                    var layers = (string.IsNullOrWhiteSpace(NeuralLayers) ? DefaultNeuralLayers : NeuralLayers)
                            .Split(new[] { ';', ',', '.', ' ', '\t', ':' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => FlexibleParser.ParseInt(x)).ToArray();

                    var options = new NeuralTyresOptions {
                        Layers = layers,
                        SeparateNetworks = NeuralSeparateNetworks,
                        TrainAverageInParallel = !NeuralSeparateNetworks,
                        TrainingRuns = NeuralTrainingRuns,
                        LearningMomentum = NeuralLearningMomentum,
                        LearningRate = NeuralLearningRate,
                        AverageAmount = NeuralAverageAmount,
                        HighPrecision = NeuralHighPrecision,
                        RandomBounds = NeuralRandomBounds,
                        ValuePadding = ValuePadding,
                        FannAlgorithm = (FannTrainingAlgorithm)(NeuralFannAlgorithm.IntValue ?? 0),
                        OverrideOutputKeys = TestSingleKey && NeuralSeparateNetworks && SelectedTestKey?.Value != null
                                ? new[] { SelectedTestKey?.Value } : null
                    };

                    var tyres = Cars.SelectMany(x => x.IsChecked ? x.Tyres.Where(y => y.IsChecked) : new CarTyres[0]).Select(x => x.Entry).Distinct(
                            TyresEntry.TyresEntryComparer).Select(x => x.ToNeuralTyresEntry()).ToList();
                    var machine = await TyresMachine.CreateAsync(tyres, options, new Progress(OnProgress), cancellation);
                    GeneratedMachine = machine;
                } catch (Exception e) when (e.IsCanceled()) { } catch (Exception e) {
                    NonfatalError.Notify("Can’t create tyres machine", e);
                }

                void OnProgress(Tuple<string, double?> p) {
                    if (cancellation.IsCancellationRequested) return;
                    var msg = $"Creating: {GetItemName(p.Item1, out _).ToSentenceMember()}…";
                    ActionExtension.InvokeInMainThread(() => progress?.Report(msg, p.Item2));
                }
            }

            private class Progress : IProgress<Tuple<string, double?>> {
                private readonly Action<Tuple<string, double?>> _callback;

                public Progress(Action<Tuple<string, double?>> callback) {
                    _callback = callback;
                }

                public void Report(Tuple<string, double?> value) {
                    _callback(value);
                }
            }
            #endregion

            #region Messing around with generated machine
            private TyresMachine _generatedMachine;

            [CanBeNull]
            public TyresMachine GeneratedMachine {
                get => _generatedMachine;
                set {
                    if (Equals(value, _generatedMachine)) return;
                    _generatedMachine = value;
                    OnPropertyChanged();
                    SelectedOutputKey = SelectedOutputKey;
                    OnPropertyChanged(nameof(DisplayTestWidth));
                    OnPropertyChanged(nameof(DisplayTestProfile));
                    UpdatePlotModel();
                    var outputKey = SelectedOutputKey;
                    OutputKeys.ReplaceEverythingBy_Direct(value?.OutputKeys.Select(GetOutputKeyEntry).ToArray() ?? new SettingEntry[0]);
                    SelectedOutputKey = outputKey;
                    _testCommand?.RaiseCanExecuteChanged();
                    _saveCommand?.RaiseCanExecuteChanged();
                }
            }

            [NotNull]
            public BetterObservableCollection<SettingEntry> OutputKeys { get; }

            public BetterListCollectionView OutputKeysView { get; }

            private static readonly SettingEntry DefaultOutputKey = GetOutputKeyEntry("ANGULAR_INERTIA");
            private SettingEntry _selectedOutputKey = DefaultOutputKey;

            public SettingEntry SelectedOutputKey {
                get => _selectedOutputKey;
                set {
                    if (OutputKeys.Contains(value) == false) {
                        value = OutputKeys.GetByIdOrDefault(_selectedOutputKey?.Value) ?? OutputKeys.FirstOrDefault() ?? DefaultOutputKey;
                    }
                    if (Equals(value, _selectedOutputKey)) return;
                    _selectedOutputKey = value;
                    OnPropertyChanged();
                    UpdatePlotModel();
                }
            }

            private TyresMachineGraphData _selectedOutputData;

            public TyresMachineGraphData SelectedOutputData {
                get => _selectedOutputData;
                set {
                    if (Equals(value, _selectedOutputData)) return;
                    _selectedOutputData = value;
                    OnPropertyChanged();
                }
            }

            private string _selectedOutputUnits;

            public string SelectedOutputUnits {
                get => _selectedOutputUnits;
                set {
                    if (Equals(value, _selectedOutputUnits)) return;
                    _selectedOutputUnits = value;
                    OnPropertyChanged();
                }
            }

            private int _selectedOutputTrackerDigits;

            public int SelectedOutputTrackerDigits {
                get => _selectedOutputTrackerDigits;
                set {
                    if (Equals(value, _selectedOutputTrackerDigits)) return;
                    _selectedOutputTrackerDigits = value;
                    OnPropertyChanged();
                }
            }

            private int _selectedOutputTitleDigits;

            public int SelectedOutputTitleDigits {
                get => _selectedOutputTitleDigits;
                set {
                    if (Equals(value, _selectedOutputTitleDigits)) return;
                    _selectedOutputTitleDigits = value;
                    OnPropertyChanged();
                }
            }

            private readonly Busy _updatePlotModelBusy = new Busy();

            // Centimetres
            private static readonly double RadiusToDisplayMultiplier = 100;

            private void UpdatePlotModel() {
                _updatePlotModelBusy.TaskDelay(async () => {
                    var machine = GeneratedMachine;
                    if (machine == null) {
                        SelectedOutputData = null;
                        return;
                    }

                    var width = machine.GetNormalization(NeuralTyresOptions.InputWidth);
                    var radius = machine.GetNormalization(NeuralTyresOptions.InputRadius);
                    var profile = machine.GetNormalization(NeuralTyresOptions.InputProfile);

                    var key = SelectedOutputKey.Value;
                    UpdateOutputType(key, out var units, out var titleDigits, out var trackerDigits, out var multiplier);

                    var list = new List<Tuple<string, double, ICollection<DataPoint>>>();
                    var extra = new List<Tuple<string, double, double, DataPoint>>();
                    var yRange = new DoubleRange();
                    var testWidth = width.Denormalize(TestWidth.Saturate());
                    var testProfile = width.Denormalize(TestProfile);

                    await Task.Run(() => {
                        if (ProfileRange == 1) {
                            list.Add(GetPiece(testWidth, TestProfile, yRange));
                        } else {
                            for (var i = 0; i < ProfileRange; i++) {
                                list.Add(GetPiece(testWidth, (i + 1d) / (ProfileRange + 1d), yRange));
                            }
                        }

                        for (var i = _tyres.Length - 1; i >= 0; i--) {
                            var tyre = _tyres[i];
                            var tyreProfile = tyre.Radius - tyre.RimRadius;
                            var value = (key.StartsWith(ThermalPrefix)
                                    ? tyre.ThermalSection.GetDouble(key.Substring(8), yRange.Minimum)
                                    : tyre.MainSection.GetDouble(key, yRange.Minimum)) * multiplier;
                            yRange.Update(value);
                            if (Math.Abs(testWidth - tyre.Width) < 0.01 && (ProfileRange > 1 || Math.Abs(testProfile - tyreProfile) < 0.01)) {
                                extra.Add(Tuple.Create(tyre.DisplaySource, tyreProfile * RadiusToDisplayMultiplier,
                                        ProfileRange > 1 ? profile.Normalize(tyreProfile) : -1d,
                                        new DataPoint(tyre.Radius * RadiusToDisplayMultiplier, value)));
                            }
                        }
                    });

                    SelectedOutputUnits = units;
                    SelectedOutputTitleDigits = titleDigits;
                    SelectedOutputTrackerDigits = trackerDigits;
                    SelectedOutputData = new TyresMachineGraphData {
                        List = list,
                        ExtraPoints = extra,
                        LeftThreshold = Radius.Minimum * RadiusToDisplayMultiplier,
                        RightThreshold = Radius.Maximum * RadiusToDisplayMultiplier,
                        YRange = yRange
                    };

                    Tuple<string, double, ICollection<DataPoint>> GetPiece(double actualWidth, double profileNormalized, DoubleRange valueRange) {
                        var result = new DataPoint[(int)(GraphSteps / Math.Sqrt(ProfileRange))];
                        for (var i = 0; i < result.Length; i++) {
                            var w = actualWidth;
                            var r = radius.Denormalize((double)i / (result.Length - 1));
                            var u = profile.Denormalize(profileNormalized.Saturate());
                            var value = machine.Conjure(key, w, r, u) * multiplier;
                            result[i].X = r * RadiusToDisplayMultiplier;
                            result[i].Y = value;
                            valueRange.Update(value);
                        }
                        return Tuple.Create($"Profile: {profile.Denormalize(profileNormalized) * 100:F2} cm",
                                profileNormalized, (ICollection<DataPoint>)result);
                    }
                }, 10);
            }

            private readonly StoredValue<double> _testWidth = Stored.Get("/CreateTyres.TestWidth", 0.5);

            public double TestWidth {
                get => _testWidth.Value;
                set {
                    value = value.Saturate();
                    if (Equals(value, _testWidth.Value)) return;
                    _testWidth.Value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTestWidth));
                    UpdatePlotModel();
                }
            }

            public string DisplayTestWidth {
                get => (GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputWidth)
                                         .Denormalize(TestWidth) * 100)?.ToString("F1", CultureInfo.CurrentUICulture) ?? "?";
                set => TestWidth = GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputWidth)
                                                    .Normalize(FlexibleParser.ParseDouble(value, 0.5d) / 100d) ?? 0.5d;
            }

            private readonly StoredValue<double> _testProfile = Stored.Get("/CreateTyres.TestProfile", 0.5);

            public double TestProfile {
                get => _testProfile.Value;
                set {
                    value = value.Saturate();
                    if (Equals(value, _testProfile.Value)) return;
                    _testProfile.Value = value;
                    OnPropertyChanged();
                    UpdatePlotModel();
                }
            }

            public string DisplayTestProfile {
                get => (GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputProfile)
                                         .Denormalize(TestProfile) * 100)?.ToString("F1", CultureInfo.CurrentUICulture) ?? "?";
                set => TestProfile = GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputProfile)
                                                      .Normalize(FlexibleParser.ParseDouble(value, 0.5d) / 100d) ?? 0.5d;
            }

            private readonly StoredValue<int> _profileRange = Stored.Get("/CreateTyres.ProfileRange", 5);

            public int ProfileRange {
                get => _profileRange.Value;
                set {
                    value = value.Clamp(1, 400);
                    if (Equals(value, _profileRange.Value)) return;
                    _profileRange.Value = value;
                    OnPropertyChanged();
                    UpdatePlotModel();
                }
            }

            private readonly StoredValue<int> _graphSteps = Stored.Get("/CreateTyres.GraphSteps", 150);

            public int GraphSteps {
                get => _graphSteps.Value;
                set {
                    value = value.Clamp(50, 5000);
                    if (Equals(value, _graphSteps.Value)) return;
                    _graphSteps.Value = value;
                    OnPropertyChanged();
                    UpdatePlotModel();
                }
            }

            public int Compare(object x, object y) {
                if (!(x is SettingEntry sx) || !(y is SettingEntry sy)) return 0;

                if (sx.DisplayName.StartsWith("Δ")) {
                    if (!sy.DisplayName.StartsWith("Δ")) return -1;
                } else if (sy.DisplayName.StartsWith("Δ")) {
                    return 1;
                }

                return string.Compare(sx.DisplayName, sy.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            }
            #endregion

            #region Save
            /*private string _suggestedMachineName;

            public string SuggestedMachineName {
                get => _suggestedMachineName;
                set {
                    if (Equals(value, _suggestedMachineName)) return;
                    _suggestedMachineName = value;
                    OnPropertyChanged();
                }
            }

            private string _machineName;

            public string MachineName {
                get => _machineName;
                set {
                    if (Equals(value, _machineName)) return;
                    _machineName = value;
                    OnPropertyChanged();
                }
            }*/

            private AsyncCommand<CarObject> _testCommand;

            public AsyncCommand<CarObject> TestCommand => _testCommand ?? (_testCommand = new AsyncCommand<CarObject>(async c => {
                try {
                    var machine = GeneratedMachine;
                    if (c?.AcdData == null || machine == null) return;

                    var data = c.AcdData;
                    var toBackup = System.IO.Path.Combine(c.Location, data.IsPacked ? DataWrapper.PackedFileName : DataWrapper.UnpackedDirectoryName);
                    var backuped = toBackup.ApartFromLast(DataWrapper.PackedFileExtension) + "_cm_nt_backup"
                            + (data.IsPacked ? DataWrapper.PackedFileExtension : "");
                    if (FileUtils.Exists(toBackup) && !FileUtils.Exists(backuped)) {
                        if (ShowMessage("Original tyres will be removed, but backup for car’s data will be made. Are you sure?", "Test tyres",
                                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

                        FileUtils.CopyRecursive(toBackup, backuped);
                    }

                    machine.CreateTyresSet(c).Save(machine.TyresVersion, c, null, null, true);

                    if (Stored.GetValue("/CreateTyres.TestOnTrack", true)) {
                        await QuickDrive.RunAsync(c);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t test tyres", e);
                }
            }, c => c != null && GeneratedMachine?.OutputKeys.Count > 1));

            private DelegateCommand _saveCommand;

            public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(() => {
                PresetsManager.Instance.SavePresetUsingDialog(NeuralMachinesKey, NeuralMachinesCategory,
                        GeneratedMachine?.ToByteArray(null) /* TODO: Icons and authorship */,
                        null /* TODO: Editing mode */);
            }, () => GeneratedMachine?.OutputKeys.Count > 1));
            #endregion

            public void Dispose() { }
        }

        public class DisplayDoubleRange : DoubleRange, INotifyPropertyChanged {
            public DisplayDoubleRange(double multiplier, string postfix) {
                _multiplier = multiplier;
                _postfix = postfix;
            }

            private readonly double _multiplier;
            private readonly string _postfix;

            public override void Reset() {
                base.Reset();
                OnPropertyChanged(nameof(Minimum));
                OnPropertyChanged(nameof(Maximum));
            }

            public double PaddedMinimum { get; private set; }
            public double PaddedMaximum { get; private set; }

            public void Apply(double padding) {
                var pad = Range * padding;
                PaddedMinimum = Minimum - pad;
                PaddedMaximum = Maximum + pad;

                var from = (PaddedMinimum * _multiplier).ToString(@"F1");
                var to = (PaddedMaximum * _multiplier).ToString(@"F1");
                DisplayValue = from == to ? from + _postfix : $@"{from}–{to}{_postfix}";

                OnPropertyChanged(nameof(Minimum));
                OnPropertyChanged(nameof(Maximum));
                OnPropertyChanged(nameof(PaddedMinimum));
                OnPropertyChanged(nameof(PaddedMaximum));
                OnPropertyChanged(nameof(DisplayValue));
            }

            public string DisplayValue { get; private set; }

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // TODO: Return created machine
        [ItemCanBeNull]
        public static async Task<object> RunAsync([CanBeNull] CarObject testCar) {
            try {
                List<CarWithTyres> list;
                using (var waiting = WaitingDialog.Create(ControlsStrings.Common_Loading)) {
                    var cancellation = waiting.CancellationToken;

                    waiting.Report("Loading cars…");
                    await CarsManager.Instance.EnsureLoadedAsync();
                    cancellation.ThrowIfCancellationRequested();

                    list = await Task.Run(() => {
                        var l = new List<CarWithTyres>();
                        var c = CarsManager.Instance.Loaded.ToList();

                        for (var i = 0; i < c.Count; i++) {
                            var car = c[i];
                            waiting.Report(car.DisplayName, i, c.Count);
                            try {
                                var tyres = car.GetTyresSets().SelectMany(x => new[] { new CarTyres(x.Front), new CarTyres(x.Rear) }).ToList();
                                if (tyres.Count > 0 && tyres[0].Entry.Version == OptionSupportedVersion) {
                                    l.Add(new CarWithTyres(car, tyres));
                                }
                            } catch (Exception e) {
                                Logging.Warning(e);
                            }
                            cancellation.ThrowIfCancellationRequested();
                        }

                        return l;
                    });

                    cancellation.ThrowIfCancellationRequested();
                }

                var dialog = new CarCreateTyresMachineDialog(testCar, list) {
                    Owner = null,
                    DoNotAttachToWaitingDialogs = true
                };

                dialog.ShowDialog();

                return null;
                // return dialog.IsResultOk ? await dialog.Model.CreateTyresMachineAsync() : null;
            } catch (Exception e) when (e.IsCanceled()) { } catch (Exception e) {
                NonfatalError.Notify("Can’t create new tyres machine", e);
            }

            return null;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var c in e.AddedItems.OfType<CarWithTyres>()) {
                c.IsChecked = true;
            }
            foreach (var c in e.RemovedItems.OfType<CarWithTyres>()) {
                c.IsChecked = false;
            }
        }
    }
}