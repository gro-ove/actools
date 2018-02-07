// ReSharper disable RedundantUsingDirective

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
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
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class CarGenerateTyresDialog {
        private ViewModel Model => (ViewModel)DataContext;

        public CarGenerateTyresDialog(CarObject target) {
            DataContext = new ViewModel(target);
            InitializeComponent();
            this.OnActualUnload(Model);

            Buttons = new[] {
                CreateExtraDialogButton(ControlsStrings.Presets_Save, new DelegateCommand(() => {
                    if (DataUpdateWarning.Warn(Model.Car)) {
                        Model.SaveCommand.Execute();
                        CloseWithResult(MessageBoxResult.OK);
                    }
                }, () => Model.SaveCommand.CanExecute()).ListenOnWeak(Model.SaveCommand)),
                CreateExtraDialogButton(UiStrings.Cancel, new DelegateCommand(() => {
                    if (!Model.Changed || ShowMessage("Discard changes?",
                            ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        CloseWithResult(MessageBoxResult.Cancel);
                    }
                }))
            };
        }

        public class TyresMachinePresetInfo : NotifyPropertyChanged {
            public ISavedPresetEntry Preset { get; }
            public Lazier<string> Description { get; }

            public TyresMachinePresetInfo(ISavedPresetEntry preset) {
                Preset = preset;
                Description = Lazier.CreateAsync(async () => {
                    return await Task.Run(() => {
                        using (var stream = new MemoryStream(preset.ReadBinaryData()))
                        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true)) {
                            var manifest = Read<JObject>("Manifest.json");
                            var version = manifest.GetIntValueOnly("tyresVersion", 0);
                            if (version < 7) {
                                throw new Exception("Unsupported tyres version: " + version);
                            }

                            var options = Read<NeuralTyresOptions>("Options.json");
                            var tyresSources = Read<NeuralTyresSource[]>("Input/Sources.json");

                            T Read<T>(string key) {
                                return JsonConvert.DeserializeObject<T>(zip.ReadString(key));
                            }

                            return $"Tyres version: {version}\n"
                                    + $"Types: {GetGenericNames(tyresSources)}\n"
                                    + $"Training runs: {options.TrainingRuns}\n"
                                    + $"Sources: {tyresSources.Length}";
                        }

                        string GetGenericNames(NeuralTyresSource[] array) {
                            return array.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Select(x => x.Key).JoinToReadableString();
                        }
                    });
                });
            }
        }

        public class ViewModel : NotifyPropertyChanged, IDraggableDestinationConverter, IComparer, IDisposable {
            public CarObject Car { get; }
            public ChangeableObservableCollection<TyresSet> Sets { get; }
            public TyresEntry OriginalTyresFront { get; }
            public TyresEntry OriginalTyresRear { get; }
            private readonly TyresSet _originalTyresSet;

            public BetterObservableCollection<TyresMachinePresetInfo> NeuralMachines { get; }

            public ViewModel(CarObject car) {
                Car = car;

                Sets = new ChangeableObservableCollection<TyresSet>(TyresSet.GetSets(car));
                if (Sets.Count == 0) {
                    throw new Exception("Can’t detect current tyres params");
                }

                SetsVersion = Sets[0].Front.Version;
                _originalTyresSet = TyresSet.GetOriginal(car) ?? Sets[0];
                OriginalTyresFront = _originalTyresSet.Front;
                OriginalTyresRear = _originalTyresSet.Rear;

                Sets.CollectionChanged += OnSetsCollectionChanged;
                Sets.ItemPropertyChanged += OnSetsItemPropertyChanged;

                Draggable.DragStarted += OnDragStarted;
                Draggable.DragEnded += OnDragEnded;

                NeuralMachines = new BetterObservableCollection<TyresMachinePresetInfo>(
                        PresetsManager.Instance.GetSavedPresets(CarCreateTyresMachineDialog.NeuralMachinesCategory).
                                       Select(x => new TyresMachinePresetInfo(x)));
                Logging.Warning(NeuralMachines.Count);
                PresetsManager.Instance.Watcher(CarCreateTyresMachineDialog.NeuralMachinesCategory).Update += OnUpdate;
            }

            private void OnUpdate(object sender, EventArgs eventArgs) {
                RefreshNeuralMachines();
            }

            private TyresMachinePresetInfo _selectedMachine;

            public TyresMachinePresetInfo SelectedMachine {
                get => _selectedMachine;
                set {
                    if (Equals(value, _selectedMachine)) return;
                    _selectedMachine = value;
                    OnPropertyChanged();

                    // TODO: Sort out:
                    _generateTyresCommand?.RaiseCanExecuteChanged();
                    GenerateTyresCommand.ExecuteAsync().Forget();
                }
            }

            private AsyncCommand _generateTyresCommand;

            public AsyncCommand GenerateTyresCommand => _generateTyresCommand ?? (_generateTyresCommand = new AsyncCommand(async () => {
                try {
                    IsGenerating = true;
                    GeneratedTyres = await Task.Run(() => {
                        var data = SelectedMachine.Preset.ReadBinaryData();
                        var machine = TyresMachine.LoadFrom(data);
                        var front = CreateTyresEntry(machine, OriginalTyresFront);
                        var rear = CreateTyresEntry(machine, OriginalTyresRear);
                        return new TyresSet(front, rear);
                    });
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t generate tyres", e);
                } finally {
                    IsGenerating = false;
                }
            }, () => SelectedMachine != null));

            private static TyresEntry CreateTyresEntry(TyresMachine machine, TyresEntry original) {
                var values = machine.Conjure(original.Width, original.Radius, original.Radius - original.RimRadius);
                return TyresEntry.CreateFromNeural(original, values);
            }

            private bool _isGenerating;

            public bool IsGenerating {
                get => _isGenerating;
                set {
                    if (Equals(value, _isGenerating)) return;
                    _isGenerating = value;
                    OnPropertyChanged();
                }
            }

            private TyresSet _generatedTyres;

            public TyresSet GeneratedTyres {
                get => _generatedTyres;
                set {
                    if (Equals(value, _generatedTyres)) return;
                    _generatedTyres = value;
                    OnPropertyChanged();
                }
            }

            public void RefreshNeuralMachines() {
                NeuralMachines.ReplaceEverythingBy_Direct(PresetsManager.Instance.GetSavedPresets(CarCreateTyresMachineDialog.NeuralMachinesCategory)
                                                                        .Select(x => new TyresMachinePresetInfo(x)));
            }

            private int _setsVersion;

            public int SetsVersion {
                get => _setsVersion;
                set {
                    if (Equals(value, _setsVersion)) return;
                    _setsVersion = value;
                    OnPropertyChanged();
                }
            }

            private TyresEntry _movingTyres;

            public TyresEntry MovingTyres {
                get => _movingTyres;
                set {
                    if (Equals(value, _movingTyres)) return;
                    _movingTyres = value;
                    OnPropertyChanged();
                }
            }

            private void OnDragStarted(object sender, DraggableMovedEventArgs e) {
                if (e.Format == TyresEntry.DraggableFormat) {
                    MovingTyres = e.Draggable as TyresEntry;
                }
            }

            private void OnDragEnded(object sender, DraggableMovedEventArgs e) {
                MovingTyres = null;
            }

            private TyresSet CreateSet(TyresEntry tyre) {
                var another = tyre.OtherEntry;
                if (another != null) {
                    return tyre.RearTyres ? new TyresSet(another, tyre) : new TyresSet(tyre, another);
                }

                return new TyresSet(tyre, tyre);
            }

            public void UpdateVersion(TyresEntry tyre) {
                if (ShowMessage(
                        $"Existing tyres are v{SetsVersion}, when this pair is v{tyre.Version}. To add it, app has to remove current tyres first. Are you sure?",
                        "Versions differ", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    SetsVersion = tyre.Version;

                    var set = CreateSet(tyre);
                    set.DefaultSet = true;
                    Sets.ReplaceEverythingBy(new[] { set });
                }
            }

            public bool PrepareTyre(TyresEntry tyre, bool rear) {
                var level = rear ? tyre.AppropriateLevelRear : tyre.AppropriateLevelFront;
                var offset = rear ? tyre.DisplayOffsetRear : tyre.DisplayOffsetFront;
                if (level > TyresAppropriateLevel.C && ShowMessage(
                        $"Compatibility level with this car: {level} ({level.GetDescription()}).\n\n{offset}.\n\nAre you sure you want to continue?",
                        ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return false;
                }

                if (SetsVersion != tyre.Version) {
                    UpdateVersion(tyre);
                    return false;
                }

                return true;
            }

            private bool _changed;

            public bool Changed {
                get => _changed;
                set {
                    if (Equals(value, _changed)) return;
                    _changed = value;
                    OnPropertyChanged();
                    _saveCommand?.RaiseCanExecuteChanged();
                }
            }

            private DelegateCommand _saveCommand;

            public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(() => {
                try {
                    if (Sets.Count == 0) {
                        throw new Exception("At least one set is required");
                    }

                    if (!Sets.All(x => x.Front.Version == SetsVersion && x.Rear.Version == SetsVersion)) {
                        throw new Exception("Versions are different");
                    }

                    var data = Car.AcdData;
                    if (data == null) {
                        throw new Exception("Data is unreadable");
                    }

                    var tyresIni = data.GetIniFile("tyres.ini");
                    tyresIni["HEADER"].Set("VERSION", SetsVersion);

                    var sets = Sets.Distinct(TyresSet.TyresSetComparer).ToList();
                    var defaultIndex = sets.FindIndex(x => x.DefaultSet);
                    tyresIni["COMPOUND_DEFAULT"].Set("INDEX", defaultIndex == -1 ? 0 : defaultIndex);

                    tyresIni["__CM_FRONT_ORIGINAL"] = OriginalTyresFront.MainSection;
                    tyresIni["__CM_THERMAL_FRONT_ORIGINAL"] = OriginalTyresFront.ThermalSection;
                    tyresIni["__CM_REAR_ORIGINAL"] = OriginalTyresRear.MainSection;
                    tyresIni["__CM_THERMAL_REAR_ORIGINAL"] = OriginalTyresRear.ThermalSection;

                    tyresIni.SetSections("FRONT", -1, sets.Select((x, i) => {
                        var curve = data.GetRawFile($@"__cm_tyre_wearcurve_front_{i}.lut");
                        curve.Content = x.Front.WearCurveData ?? "";
                        curve.Save();

                        return new IniFileSection(data, x.Front.MainSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["WEAR_CURVE"] = curve.Name,
                            ["__CM_SOURCE_ID"] = x.Front.SourceCarId
                        };
                    }));

                    tyresIni.SetSections("REAR", -1, sets.Select((x, i) => {
                        var curve = data.GetRawFile($@"__cm_tyre_wearcurve_rear_{i}.lut");
                        curve.Content = x.Rear.WearCurveData ?? "";
                        curve.Save();

                        return new IniFileSection(data, x.Rear.MainSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["WEAR_CURVE"] = curve.Name,
                            ["__CM_SOURCE_ID"] = x.Rear.SourceCarId
                        };
                    }));

                    tyresIni.SetSections("THERMAL_FRONT", -1, sets.Select((x, i) => {
                        var curve = data.GetRawFile($@"__cm_tyre_perfcurve_front_{i}.lut");
                        curve.Content = x.Front.PerformanceCurveData ?? "";
                        curve.Save();

                        return new IniFileSection(data, x.Front.ThermalSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["PERFORMANCE_CURVE"] = curve.Name
                        };
                    }));

                    tyresIni.SetSections("THERMAL_REAR", -1, sets.Select((x, i) => {
                        var curve = data.GetRawFile($@"__cm_tyre_perfcurve_rear_{i}.lut");
                        curve.Content = x.Rear.PerformanceCurveData ?? "";
                        curve.Save();

                        return new IniFileSection(data, x.Rear.ThermalSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["PERFORMANCE_CURVE"] = curve.Name
                        };
                    }));

                    tyresIni.Save(true);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save changes", e);
                }
            }, () => Changed));

            private void OnSetsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                Changed = true;
                UpdateIndeces();
                UpdateDublicates();
            }

            private bool _ignore;

            private void OnSetsItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(TyresSet.Index):
                    case nameof(TyresSet.Position):
                    case nameof(TyresSet.Dublicate):
                    case nameof(TyresSet.CanBeDeleted):
                    case nameof(TyresSet.DifferentTyres):
                        return;
                    case nameof(TyresSet.IsDeleted):
                        Sets.Remove((TyresSet)sender);
                        break;
                    case nameof(TyresSet.Front):
                    case nameof(TyresSet.Rear):
                        UpdateDublicates();
                        break;
                    case nameof(TyresSet.DefaultSet):
                        if (_ignore) return;
                        _ignore = true;
                        foreach (var set in Sets) {
                            set.DefaultSet = ReferenceEquals(set, sender);
                        }
                        _ignore = false;
                        break;
                }

                Changed = true;
            }

            private void UpdateIndeces() {
                if (Sets.Count == 1) {
                    var set = Sets[0];
                    set.Index = 0;
                    set.CanBeDeleted = false;
                } else {
                    for (var i = 0; i < Sets.Count; i++) {
                        var set = Sets[i];
                        set.Index = i;
                        set.CanBeDeleted = true;
                    }
                }
            }

            private void UpdateDublicates() {
                if (Sets.Count == 0) return;
                Sets[0].Dublicate = false;

                for (var i = 1; i < Sets.Count; i++) {
                    var set = Sets[i];
                    var dublicate = false;

                    for (var j = 0; j < i; j++) {
                        var s = Sets[j];
                        if (TyresSet.TyresSetComparer.Equals(s, set)) {
                            dublicate = true;
                            break;
                        }
                    }

                    set.Dublicate = dublicate;
                }
            }

            public int Compare(object x, object y) {
                var xt = (TyresEntry)x;
                var yt = (TyresEntry)y;

                if (xt == null) return yt == null ? 0 : 1;
                if (yt == null) return -1;

                var xm = xt.AppropriateLevelFront < xt.AppropriateLevelRear ? xt.AppropriateLevelFront : xt.AppropriateLevelRear;
                var ym = yt.AppropriateLevelFront < yt.AppropriateLevelRear ? yt.AppropriateLevelFront : yt.AppropriateLevelRear;
                if (xm != ym) return xm - ym;

                var xx = xt.AppropriateLevelFront > xt.AppropriateLevelRear ? xt.AppropriateLevelFront : xt.AppropriateLevelRear;
                var yx = yt.AppropriateLevelFront > yt.AppropriateLevelRear ? yt.AppropriateLevelFront : yt.AppropriateLevelRear;
                if (xx != yx) return xx - yx;

                var nx = (xt.Source?.DisplayName ?? xt.SourceCarId).InvariantCompareTo(yt.Source?.DisplayName ?? yt.SourceCarId);
                if (nx != 0) return nx;

                var mx = xt.Name.InvariantCompareTo(yt.Name);
                if (mx != 0) return mx;

                return -xt.RearTyres.CompareTo(yt.RearTyres);
            }

            public object Convert(IDataObject data) {
                if (data.GetData(TyresSet.DraggableFormat) is TyresSet set) return set;
                if (data.GetData(TyresEntry.DraggableFormat) is TyresEntry tyre) {
                    if (SetsVersion != tyre.Version) {
                        UpdateVersionLater(tyre);
                        return null;
                    }

                    return CreateSet(tyre);
                }

                return null;
            }

            private async void UpdateVersionLater(TyresEntry tyre) {
                await Task.Delay(1);
                UpdateVersion(tyre);
            }

            public void Dispose() {
                PresetsManager.Instance.Watcher(CarCreateTyresMachineDialog.NeuralMachinesCategory).Update += OnUpdate;
                Draggable.DragStarted -= OnDragStarted;
                Draggable.DragEnded -= OnDragEnded;
            }
        }

        public static async Task<bool> RunAsync(CarObject target) {
            try {
                /*ChangeableObservableCollection<TyresMachineInfo> machines;
                using (WaitingDialog.Create(ControlsStrings.Common_Loading)) {
                    machines = await TyresMachineInfo.LoadMachinesAsync();
                }*/

                var dialog = new CarGenerateTyresDialog(target);
                dialog.ShowDialog();
                return dialog.IsResultOk;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t replace tyres", e);
                return false;
            }
        }

        private async void OnFrontSlotDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(TyresEntry.DraggableFormat) is TyresEntry tyre) {
                e.Handled = true;

                if (((FrameworkElement)sender).DataContext is TyresSet set) {
                    e.Effects = DragDropEffects.Copy;

                    await Task.Delay(1);
                    if (Model.PrepareTyre(tyre, false)) {
                        if (Keyboard.Modifiers == ModifierKeys.Shift && _draggingFrom != null) {
                            if (_draggingFromRearSlot) {
                                _draggingFrom.Rear = set.Front;
                            } else {
                                _draggingFrom.Front = set.Front;
                            }
                        }

                        set.Front = tyre;
                    }
                }
            }
        }

        private async void OnRearSlotDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(TyresEntry.DraggableFormat) is TyresEntry tyre) {
                e.Handled = true;

                if (((FrameworkElement)sender).DataContext is TyresSet set) {
                    e.Effects = DragDropEffects.Copy;

                    await Task.Delay(1);
                    if (Model.PrepareTyre(tyre, true)) {
                        if (Keyboard.Modifiers == ModifierKeys.Shift && _draggingFrom != null) {
                            if (_draggingFromRearSlot) {
                                _draggingFrom.Rear = set.Rear;
                            } else {
                                _draggingFrom.Front = set.Rear;
                            }
                        }

                        set.Rear = tyre;
                    }
                }
            }
        }

        private TyresSet _draggingFrom;
        private bool _draggingFromRearSlot;

        private void OnFrontSlotMouseDown(object sender, MouseButtonEventArgs e) {
            if (((FrameworkElement)sender).DataContext is TyresSet set) {
                _draggingFrom = set;
                _draggingFromRearSlot = false;
            }
        }

        private void OnRearSlotMouseDown(object sender, MouseButtonEventArgs e) {
            if (((FrameworkElement)sender).DataContext is TyresSet set) {
                _draggingFrom = set;
                _draggingFromRearSlot = true;
            }
        }

        private async void OnNewTyresMachineButtonClick(object sender, RoutedEventArgs e) {
            var machine = await CarCreateTyresMachineDialog.RunAsync();
            if (machine != null) {
                Model.RefreshNeuralMachines();
            }
        }
    }
}