// ReSharper disable RedundantUsingDirective

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
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
        public static int OptionSupportedVersion = 10;

        public static readonly string TrainingParamsKey = "Tyres Generation";
        public static readonly PresetsCategory TrainingParamsCategory = new PresetsCategory(Path.Combine("Tyres Generation", "Neural Params"));

        public static readonly string ExampleCarsParamsKey = "Tyres Examples";
        public static readonly PresetsCategory ExampleCarsParamsCategory = new PresetsCategory(Path.Combine("Tyres Generation", "Examples"));

        public static readonly string NeuralMachinesKey = "Tyres Machines";
        public static readonly PresetsCategory NeuralMachinesCategory = new PresetsCategory(Path.Combine("Tyres Generation", "Tyres Machines"), ".zip");

        private static readonly SettingEntry DefaultOutputKey = GetOutputKeyEntry("ANGULAR_INERTIA");

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

            foreach (var s in Model.ExampleCarsModel.CarsList.Where(x => x.IsChecked)) {
                CarsListBox.SelectedItems.Add(s);
            }

            CarsListBox.SelectionChanged += OnSelectionChanged;
            Model.ExampleCarsModel.ListFilterUpdate += OnListFilterUpdate;
            Model.ExampleCarsModel.PropertyChanged += OnModelPropertyChanged;
        }

        private class ViewModel : NotifyPropertyChanged, IDisposable {
            public TrainingViewModel TrainingModel { get; }
            public ExampleCarsViewModel ExampleCarsModel { get; }

            public ViewModel(List<CarWithTyres> cars) {
                OutputKeys = new BetterObservableCollection<SettingEntry>();
                OutputKeysView = new BetterListCollectionView(OutputKeys) {
                    GroupDescriptions = { new PropertyGroupDescription(nameof(SettingEntry.Tag)) },
                    CustomSort = KeyComparer.Instance
                };

                TrainingModel = new TrainingViewModel();
                ExampleCarsModel = new ExampleCarsViewModel(cars);
                ExampleCarsModel.InvalidatedPlotModel += OnInvalidatedPlotModel;
            }

            private void OnInvalidatedPlotModel(object sender, EventArgs eventArgs) {
                UpdatePlotModel();
            }

            #region Generation
            private AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> _createTyresMachineCommand;

            public AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> CreateTyresMachineCommand => _createTyresMachineCommand
                    ?? (_createTyresMachineCommand = new AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>>(
                            CreateTyresMachineAsync, c => ExampleCarsModel.UniqueTyresCount > 1))
                            .ListenOn(ExampleCarsModel, nameof(ExampleCarsModel.UniqueTyresCount));

            private bool _isGenerating;

            public bool IsGenerating {
                get => _isGenerating;
                set {
                    if (Equals(value, _isGenerating)) return;
                    _isGenerating = value;
                    OnPropertyChanged();
                }
            }

            private async Task CreateTyresMachineAsync(Tuple<IProgress<AsyncProgressEntry>, CancellationToken> tuple) {
                var progress = tuple?.Item1;
                var cancellation = tuple?.Item2 ?? CancellationToken.None;
                progress?.Report(new AsyncProgressEntry("Initialization…", 0.001d));

                try {
                    IsGenerating = true;

                    var layers = (string.IsNullOrWhiteSpace(TrainingModel.Layers) ? DefaultNeuralLayers : TrainingModel.Layers)
                            .Split(new[] { ';', ',', '.', ' ', '\t', ':' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => FlexibleParser.ParseInt(x)).ToArray();

                    var options = new NeuralTyresOptions {
                        Layers = layers,
                        SeparateNetworks = TrainingModel.SeparateNetworks,
                        TrainAverageInParallel = !TrainingModel.SeparateNetworks,
                        TrainingRuns = TrainingModel.TrainingRuns,
                        LearningMomentum = TrainingModel.LearningMomentum,
                        LearningRate = TrainingModel.LearningRate,
                        AverageAmount = TrainingModel.AverageAmount,
                        HighPrecision = TrainingModel.HighPrecision,
                        RandomBounds = TrainingModel.RandomBounds,
                        ValuePadding = ExampleCarsModel.ValuePadding,
                        FannAlgorithm = (FannTrainingAlgorithm)(TrainingModel.FannAlgorithm.IntValue ?? 0),
                        OverrideOutputKeys = ExampleCarsModel.TestSingleKey && TrainingModel.SeparateNetworks && ExampleCarsModel.SelectedTestKey?.Value != null
                                ? new[] { ExampleCarsModel.SelectedTestKey?.Value } : null
                    };

                    var tyres = ExampleCarsModel.CarsList.SelectMany(x => x.IsChecked ? x.Tyres.Where(y => y.IsChecked) : new CarTyres[0])
                                                .Select(x => x.Entry).Distinct(TyresEntry.TyresEntryComparer).Select(x => x.ToNeuralTyresEntry()).ToList();
                    GeneratedMachine = await TyresMachine.CreateAsync(tyres, options, new Progress(OnProgress), cancellation);
                } catch (Exception e) when (e.IsCanceled()) { } catch (Exception e) {
                    NonfatalError.Notify("Can’t create tyres machine", e);
                } finally {
                    IsGenerating = false;
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
                    var outputKey = SelectedOutputKey;
                    OutputKeys.ReplaceEverythingBy_Direct(value?.OutputKeys.Select(GetOutputKeyEntry).ToArray() ?? new SettingEntry[0]);
                    SelectedOutputKey = OutputKeys.GetByIdOrDefault(outputKey?.Value);
                    _testCommand?.RaiseCanExecuteChanged();
                    _saveCommand?.RaiseCanExecuteChanged();
                    UpdatePlotModel();
                }
            }

            [NotNull]
            public BetterObservableCollection<SettingEntry> OutputKeys { get; }

            public BetterListCollectionView OutputKeysView { get; }

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
                _updatePlotModelBusy.TaskYield(async () => {
                    var machine = GeneratedMachine;
                    if (machine == null) {
                        SelectedOutputData = null;
                        return;
                    }

                    var width = machine.GetNormalization(NeuralTyresOptions.InputWidth);
                    var radius = machine.GetNormalization(NeuralTyresOptions.InputRadius);
                    var profile = machine.GetNormalization(NeuralTyresOptions.InputProfile);

                    var testWidthDenormalized = width.Denormalize(TestWidth.Saturate());
                    var testRadiusDenormalized = radius.Denormalize(TestRadius.Saturate());
                    var testProfileDenormalized = profile.Denormalize(TestProfile.Saturate());

                    var key = SelectedOutputKey.Value;
                    UpdateOutputType(key, out var units, out var titleDigits, out var trackerDigits, out var multiplier);

                    var list = new List<Tuple<string, double, ICollection<DataPoint>>>();
                    var extra = new List<Tuple<string, double, double, DataPoint>>();
                    var yRange = new DoubleRange();
                    string generatedData = null;

                    await Task.Run(() => {
                        var tyres = machine.CreateTyresEntry(testWidthDenormalized, testRadiusDenormalized, testProfileDenormalized);
                        var name = tyres.MainSection.GetNonEmpty("NAME")?.Replace(' ', '_').ToLowerInvariant() ?? @"tyres";
                        tyres.MainSection.Set("WEAR_CURVE", $"{name}_front.lut");
                        tyres.ThermalSection.Set("PERFORMANCE_CURVE", $"tcurve_{name}.lut");
                        generatedData = new IniFile {
                            ["FRONT"] = tyres.MainSection,
                            ["THERMAL_FRONT"] = tyres.ThermalSection
                        }.ToString();

                        var profileRange = 5; // ProfileRange;
                        if (profileRange > 1) {
                            for (var i = 0; i < profileRange; i++) {
                                list.Add(GetPiece(testWidthDenormalized, false, (i + 1d) / (profileRange + 1d), yRange));
                            }
                        }

                        list.Add(GetPiece(testWidthDenormalized, true, TestProfile, yRange));

                        for (var i = ExampleCarsModel.SelectedTyres.Length - 1; i >= 0; i--) {
                            var tyre = ExampleCarsModel.SelectedTyres[i];
                            var tyreProfile = tyre.Radius - tyre.RimRadius;
                            var value = (key.StartsWith(ThermalPrefix)
                                    ? tyre.ThermalSection.GetDouble(key.Substring(8), yRange.Minimum)
                                    : tyre.MainSection.GetDouble(key, yRange.Minimum)) * multiplier;
                            yRange.Update(value);
                            if (Math.Abs(testWidthDenormalized - tyre.Width) < 0.01
                                    && (profileRange > 1 || Math.Abs(testProfileDenormalized - tyreProfile) < 0.01)) {
                                extra.Add(Tuple.Create(tyre.DisplaySource, tyreProfile * RadiusToDisplayMultiplier,
                                        profileRange > 1 ? profile.Normalize(tyreProfile) : -1d,
                                        new DataPoint(tyre.Radius * RadiusToDisplayMultiplier, value)));
                            }
                        }

                        var testValue = machine.Conjure(key, testWidthDenormalized, testRadiusDenormalized, testProfileDenormalized)
                                * multiplier;
                        extra.Add(Tuple.Create("Test", testProfileDenormalized * RadiusToDisplayMultiplier, -1d,
                                new DataPoint(testRadiusDenormalized * RadiusToDisplayMultiplier, testValue)));
                    });

                    GeneratedTyresData = generatedData;
                    SelectedOutputUnits = units;
                    SelectedOutputTitleDigits = titleDigits;
                    SelectedOutputTrackerDigits = trackerDigits;
                    SelectedOutputData = new TyresMachineGraphData {
                        List = list,
                        ExtraPoints = extra,
                        LeftThreshold = ExampleCarsModel.Radius.Minimum * RadiusToDisplayMultiplier,
                        RightThreshold = ExampleCarsModel.Radius.Maximum * RadiusToDisplayMultiplier,
                        YRange = yRange
                    };

                    Tuple<string, double, ICollection<DataPoint>> GetPiece(double actualWidth, bool mainCurve, double profileNormalized, DoubleRange valueRange) {
                        // var result = new DataPoint[(int)(GraphSteps / Math.Sqrt(ProfileRange))];
                        var result = new DataPoint[100];
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
                                mainCurve ? -1d : profileNormalized, (ICollection<DataPoint>)result);
                    }
                });
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
                    OnPropertyChanged(nameof(DisplayTestProfile));
                    UpdatePlotModel();
                }
            }

            public string DisplayTestProfile {
                get => (GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputProfile)
                                         .Denormalize(TestProfile) * 100)?.ToString("F2", CultureInfo.CurrentUICulture) ?? "?";
                set => TestProfile = GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputProfile)
                                                      .Normalize(FlexibleParser.ParseDouble(value, 0.5d) / 100d) ?? 0.5d;
            }

            private readonly StoredValue<double> _testRadius = Stored.Get("/CreateTyres.TestRadius", 0.5);

            public double TestRadius {
                get => _testRadius.Value;
                set {
                    value = value.Saturate();
                    if (Equals(value, _testRadius.Value)) return;
                    _testRadius.Value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTestRadius));
                    UpdatePlotModel();
                }
            }

            public string DisplayTestRadius {
                get => (GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputRadius)
                                         .Denormalize(TestRadius) * 100)?.ToString("F2", CultureInfo.CurrentUICulture) ?? "?";
                set => TestRadius = GeneratedMachine?.GetNormalization(NeuralTyresOptions.InputRadius)
                                                     .Normalize(FlexibleParser.ParseDouble(value, 0.5d) / 100d) ?? 0.5d;
            }

            private string _generatedTyresData;

            public string GeneratedTyresData {
                get => _generatedTyresData;
                set {
                    if (Equals(value, _generatedTyresData)) return;
                    _generatedTyresData = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Save
            private AsyncCommand<CarObject> _testCommand;

            public AsyncCommand<CarObject> TestCommand => _testCommand ?? (_testCommand = new AsyncCommand<CarObject>(async c => {
                try {
                    var machine = GeneratedMachine;
                    if (c?.AcdData == null || machine == null) return;

                    var data = c.AcdData;
                    var toBackup = Path.Combine(c.Location, data.IsPacked ? DataWrapper.PackedFileName : DataWrapper.UnpackedDirectoryName);
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

        private void OnListFilterUpdate(object sender, EventArgs e) {
            _syncSelectionBusy.DoDelay(SyncSelectionNow, 100);
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(ExampleCarsViewModel.CarsFilter)) {
                // SyncSelection();
            }
        }

        private readonly Busy _syncSelectionBusy = new Busy();

        private void SyncSelection() {
            _syncSelectionBusy.Yield(SyncSelectionNow);
        }

        private void SyncSelectionNow() {
            /*CarsListBox.SelectedItems.Clear();
            foreach (var car in Model.ExampleCarsModel.CarsView) {
                CarsListBox.SelectedItems.Add(car);
            }*/
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            _syncSelectionBusy.Do(() => {
                foreach (CarWithTyres car in e.RemovedItems) {
                    car.IsChecked = false;
                }
                foreach (CarWithTyres car in e.AddedItems) {
                    car.IsChecked = true;
                }
            });
        }
    }
}