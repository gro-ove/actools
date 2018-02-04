using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Tyres;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Attached;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class CarReplaceTyresDialog {
        private CarReplaceTyresDialog(CarObject target, List<TyresSet> sets, List<TyresEntry> tyres) {
            DataContext = new ViewModel(target, sets, tyres);
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

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged, IDraggableDestinationConverter, IComparer, IDisposable {
            public CarObject Car { get; }

            public ChangeableObservableCollection<TyresSet> Sets { get; }

            public BetterObservableCollection<TyresEntry> Tyres { get; }

            public BetterListCollectionView TyresView { get; }

            private int _setsVersion;

            public int SetsVersion {
                get => _setsVersion;
                set {
                    if (Equals(value, _setsVersion)) return;
                    _setsVersion = value;
                    OnPropertyChanged();
                }
            }

            public TyresEntry OriginalTyresFront { get; }

            public TyresEntry OriginalTyresRear { get; }

            private AsyncCommand _changeCarsFilterCommand;

            public AsyncCommand ChangeCarsFilterCommand => _changeCarsFilterCommand ?? (_changeCarsFilterCommand = new AsyncCommand(async () => {
                var newFilter = Prompt.Show("New filter for source cars:", "Source cars filter",
                        SettingsHolder.Content.CarReplaceTyresDonorFilter, "*",
                        suggestions: ValuesStorage.GetStringList("__CarReplaceTyresDonorFilters"))?.Trim();

                switch (newFilter) {
                    case null:
                        return;
                    case "":
                        newFilter = "*";
                        break;
                }

                var list = await TyresEntry.GetList(newFilter);
                if (list == null) return;

                foreach (var entry in list) {
                    entry.SetAppropriateLevel(_originalTyresSet);
                }

                Tyres.ReplaceEverythingBy(list);
                SettingsHolder.Content.CarReplaceTyresDonorFilter = newFilter;
            }));

            private readonly TyresSet _originalTyresSet;

            public ViewModel(CarObject car, List<TyresSet> sets, List<TyresEntry> tyres) {
                Car = car;
                Tyres = new BetterObservableCollection<TyresEntry>(tyres);
                TyresView = new BetterListCollectionView(Tyres);
                Sets = new ChangeableObservableCollection<TyresSet>(sets);

                var firstSet = Sets.FirstOrDefault();
                if (firstSet == null) {
                    throw new Exception("Can’t detect current tyres params");
                }

                SetsVersion = firstSet.Front.Version;

                _originalTyresSet = TyresSet.GetOriginal(car) ?? firstSet;
                OriginalTyresFront = _originalTyresSet.Front;
                OriginalTyresRear = _originalTyresSet.Rear;

                foreach (var set in Sets) {
                    set.Front.SetAppropriateLevel(_originalTyresSet);
                    set.Rear.SetAppropriateLevel(_originalTyresSet);
                }

                foreach (var entry in tyres) {
                    entry.SetAppropriateLevel(_originalTyresSet);
                }

                Sets.CollectionChanged += OnSetsCollectionChanged;
                Sets.ItemPropertyChanged += OnSetsItemPropertyChanged;

                UpdateIndeces();

                using (TyresView.DeferRefresh()) {
                    TyresView.CustomSort = this;
                    TyresView.Filter = o => {
                        if (_filterObj == null) return true;
                        return o is TyresEntry t && _filterObj.Test(t);
                    };
                }

                Draggable.DragStarted += OnDragStarted;
                Draggable.DragEnded += OnDragEnded;
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

            private string _filterValue;
            private IFilter<TyresEntry> _filterObj;

            public string FilterValue {
                get => _filterValue;
                set {
                    if (Equals(value, _filterValue)) return;
                    _filterValue = value;
                    _filterObj = string.IsNullOrWhiteSpace(value) ? null : Filter.Create(TyresEntryTester.Instance, value);
                    OnPropertyChanged();
                    TyresView.Refresh();
                }
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

            private TyresSet CreateSet(TyresEntry tyre) {
                var another = tyre.OtherEntry ?? Tyres.FirstOrDefault(x => ReferenceEquals(x.Source, tyre.Source) && x.Name == tyre.Name &&
                        x.RearTyres != tyre.RearTyres);
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

            public void Dispose() {
                Draggable.DragStarted -= OnDragStarted;
                Draggable.DragEnded -= OnDragEnded;
            }
        }

        public static async Task<bool> Run(CarObject target) {
            try {
                var sets = TyresSet.GetSets(target).ToList();
                if (sets.Count == 0) {
                    throw new Exception("Can’t detect current tyres params");
                }

                List<TyresEntry> list;
                using (WaitingDialog.Create(ControlsStrings.Common_Loading)) {
                    list = await TyresEntry.GetList(SettingsHolder.Content.CarReplaceTyresDonorFilter);
                    if (list == null) return false;
                }

                var dialog = new CarReplaceTyresDialog(target, sets, list);
                dialog.ShowDialog();
                return dialog.IsResultOk;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t replace tyres", e);
                return false;
            }
        }

        [ValueConversion(typeof(RealismLevel), typeof(SolidColorBrush))]
        private class AppropriateLevelToColorConverterInner : IValueConverter {
            private static Color GetColor(TyresAppropriateLevel? level) {
                switch (level) {
                    case TyresAppropriateLevel.A:
                        return Colors.LimeGreen;

                    case TyresAppropriateLevel.B:
                        return Colors.Yellow;

                    case TyresAppropriateLevel.C:
                        return Colors.Orange;

                    case TyresAppropriateLevel.D:
                        return Colors.OrangeRed;

                    case TyresAppropriateLevel.E:
                        return Colors.Red;

                    case TyresAppropriateLevel.F:
                        return Colors.SaddleBrown;

                    default:
                        return Colors.LightGray;
                }
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return new SolidColorBrush(GetColor(value as TyresAppropriateLevel?));
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter AppropriateLevelToColorConverter { get; } = new AppropriateLevelToColorConverterInner();

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
    }
}