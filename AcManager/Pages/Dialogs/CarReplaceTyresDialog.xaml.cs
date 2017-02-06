using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class CarReplaceTyresDialog {
        public static bool OptionRoundNicely = true;
        public static string OptionTyresDonorsFilter = "k+";

        private CarReplaceTyresDialog(CarObject target, List<TyresSet> sets, List<TyresEntry> tyres) {
            DataContext = new ViewModel(target, sets, tyres);
            InitializeComponent();
            
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

        public class ViewModel : NotifyPropertyChanged, IDraggableDestinationConverter, IComparer {
            public CarObject Car { get; }

            public ChangeableObservableCollection<TyresSet> Sets { get; }

            public List<TyresEntry> Tyres { get; }

            public BetterListCollectionView TyresView { get; }

            private int _setsVersion;

            public int SetsVersion {
                get { return _setsVersion; }
                set {
                    if (Equals(value, _setsVersion)) return;
                    _setsVersion = value;
                    OnPropertyChanged();
                }
            }

            public TyresEntry OriginalTyresFront { get; }

            public TyresEntry OriginalTyresRear { get; }

            public ViewModel(CarObject car, List<TyresSet> sets, List<TyresEntry> tyres) {
                Car = car;
                Tyres = tyres;
                TyresView = new BetterListCollectionView(Tyres);
                Sets = new ChangeableObservableCollection<TyresSet>(sets);

                var firstSet = Sets.FirstOrDefault();
                if (firstSet == null) {
                    throw new Exception("Can’t detect current tyres params");
                }

                SetsVersion = firstSet.Front.Version;

                var original = GetOriginal(car) ?? firstSet;
                OriginalTyresFront = original.Front;
                OriginalTyresRear = original.Rear;

                foreach (var set in Sets) {
                    set.Front.SetAppropriateLevel(original);
                    set.Rear.SetAppropriateLevel(original);
                }

                foreach (var entry in tyres) {
                    entry.SetAppropriateLevel(original);
                }

                Sets.CollectionChanged += OnSetsCollectionChanged;
                Sets.ItemPropertyChanged += OnSetsItemPropertyChanged;

                UpdateIndeces();

                using (TyresView.DeferRefresh()) {
                    TyresView.CustomSort = this;
                    TyresView.Filter = o => {
                        if (_filterObj == null) return true;
                        var t = o as TyresEntry;
                        return t != null && _filterObj.Test(t);
                    };
                }

                Draggable.DragStarted += OnDragStarted;
                Draggable.DragEnded += OnDragEnded;
            }

            private TyresEntry _movingTyres;

            public TyresEntry MovingTyres {
                get { return _movingTyres; }
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
                get { return _changed; }
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

                        return new IniFileSection(x.Front.MainSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["WEAR_CURVE"] = curve.Name,
                            ["__CM_SOURCE_ID"] = x.Front.SourceId
                        };
                    }));

                    tyresIni.SetSections("REAR", -1, sets.Select((x, i) => {
                        var curve = data.GetRawFile($@"__cm_tyre_wearcurve_rear_{i}.lut");
                        curve.Content = x.Rear.WearCurveData ?? "";
                        curve.Save();

                        return new IniFileSection(x.Rear.MainSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["WEAR_CURVE"] = curve.Name,
                            ["__CM_SOURCE_ID"] = x.Rear.SourceId
                        };
                    }));
                    
                    tyresIni.SetSections("THERMAL_FRONT", -1, sets.Select((x, i) => {
                        var curve = data.GetRawFile($@"__cm_tyre_perfcurve_front_{i}.lut");
                        curve.Content = x.Front.PerformanceCurveData ?? "";
                        curve.Save();

                        return new IniFileSection(x.Front.ThermalSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["PERFORMANCE_CURVE"] = curve.Name
                        };
                    }));

                    tyresIni.SetSections("THERMAL_REAR", -1, sets.Select((x, i) => {
                        var curve = data.GetRawFile($@"__cm_tyre_perfcurve_rear_{i}.lut");
                        curve.Content = x.Rear.PerformanceCurveData ?? "";
                        curve.Save();

                        return new IniFileSection(x.Rear.ThermalSection) {
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
                get { return _filterValue; }
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

                var xm = xt.AppropriateLevelFront < xt.AppropriateLevelRear ? xt.AppropriateLevelFront : xt.AppropriateLevelRear;
                var ym = yt.AppropriateLevelFront < yt.AppropriateLevelRear ? yt.AppropriateLevelFront : yt.AppropriateLevelRear;
                if (xm != ym) return xm - ym;

                var xx = xt.AppropriateLevelFront > xt.AppropriateLevelRear ? xt.AppropriateLevelFront : xt.AppropriateLevelRear;
                var yx = yt.AppropriateLevelFront > yt.AppropriateLevelRear ? yt.AppropriateLevelFront : yt.AppropriateLevelRear;
                if (xx != yx) return xx - yx;

                var nx = (xt.Source?.DisplayName ?? xt.SourceId).InvariantCompareTo(yt.Source?.DisplayName ?? yt.SourceId);
                if (nx != 0) return nx;

                var mx = xt.Name.InvariantCompareTo(yt.Name);
                if (mx != 0) return mx;

                return -xt.RearTyres.CompareTo(yt.RearTyres);
            }

            public object Convert(IDataObject data) {
                var set = data.GetData(TyresSet.DraggableFormat) as TyresSet;
                if (set != null) return set;

                var tyre = data.GetData(TyresEntry.DraggableFormat) as TyresEntry;
                if (tyre != null) {
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
                        "Versions Differ", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
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
        }

        [CanBeNull]
        private static TyresSet GetOriginal(CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) return null;

            var front = TyresEntry.Create(car, @"__CM_FRONT_ORIGINAL", true);
            var rear = TyresEntry.Create(car, @"__CM_REAR_ORIGINAL", true);
            if (front != null && rear != null) {
                return new TyresSet(front, rear);
            } else {
                return null;
            }
        }

        private static IEnumerable<Tuple<TyresEntry, TyresEntry>> GetTyres(CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) yield break;

            for (var i = 0; i < 9999; i++) {
                var front = TyresEntry.CreateFront(car, i, false);
                var rear = TyresEntry.CreateRear(car, i, false);

                if (front == null && rear == null) break;

                if (front != null) front.OtherEntry = rear;
                if (rear != null) rear.OtherEntry = front;
                yield return Tuple.Create(front, rear);
            }
        }

        private static IEnumerable<TyresSet> GetSets(CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) return new TyresSet[0];

            var defaultSet = tyres["COMPOUND_DEFAULT"].GetInt("INDEX", 0);
            return GetTyres(car).Where(x => x.Item1 != null && x.Item2 != null).Select((x, i) => new TyresSet(x.Item1, x.Item2) {
                DefaultSet = i == defaultSet
            });
        }

        public static async Task<bool> Run(CarObject target) {
            try {
                var sets = GetSets(target).ToList();
                if (sets.Count == 0) {
                    ShowMessage("Can’t detect current tyres params", ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OK);
                    return false;
                }

                var wrappers = CarsManager.Instance.WrappersList.ToList();
                var list = new List<TyresEntry>(wrappers.Count);
                var filter = Filter.Create(CarObjectTester.Instance, OptionTyresDonorsFilter);

                using (var waiting = new WaitingDialog("Getting a list of tyres…")) {
                    for (var i = 0; i < wrappers.Count; i++) {
                        if (waiting.CancellationToken.IsCancellationRequested) return false;

                        var wrapper = wrappers[i];
                        var car = (CarObject)await wrapper.LoadedAsync();
                        waiting.Report(new AsyncProgressEntry(car.DisplayName, i, wrappers.Count));

                        if (!filter.Test(car) || car.AcdData == null) continue;
                        var tyres = car.AcdData.GetIniFile("tyres.ini");

                        var version = tyres["HEADER"].GetInt("VERSION", -1);
                        if (version < 4) continue;

                        foreach (var tuple in GetTyres(car)) {
                            if (list.Contains(tuple.Item1, TyresEntry.TyresEntryComparer)) {
                                if (!list.Contains(tuple.Item2, TyresEntry.TyresEntryComparer)) {
                                    list.Add(tuple.Item2);
                                }
                            } else {
                                if (TyresEntry.TyresEntryComparer.Equals(tuple.Item1, tuple.Item2)) {
                                    list.Add(tuple.Item1);
                                    tuple.Item1.BothTyres = true;
                                } else if (!list.Contains(tuple.Item2, TyresEntry.TyresEntryComparer)) {
                                    list.Add(tuple.Item1);
                                    list.Add(tuple.Item2);
                                }
                            }
                        }

                        if (i % 3 == 0) {
                            await Task.Delay(10);
                        }
                    }
                }

                var dialog = new CarReplaceTyresDialog(target, sets, list);
                dialog.ShowDialog();
                return dialog.IsResultOk;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t replace tyres", e);
                return false;
            }
        }

        public sealed class TyresSet : Displayable, IDraggable {
            private int _index;

            public int Index {
                get { return _index; }
                set {
                    if (Equals(value, _index)) return;
                    _index = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Position));
                }
            }

            public int Position => Index + 1;

            private bool _dublicate;

            public bool Dublicate {
                get { return _dublicate; }
                set {
                    if (Equals(value, _dublicate)) return;
                    _dublicate = value;
                    OnPropertyChanged();
                }
            }

            private bool _differentTyres;

            public bool DifferentTyres {
                get { return _differentTyres; }
                set {
                    if (Equals(value, _differentTyres)) return;
                    _differentTyres = value;
                    OnPropertyChanged();
                }
            }

            private void UpdateDifferentTyres() {
                DifferentTyres = _front?.Name != _rear?.Name;
            }

            private bool _defaultSet;

            public bool DefaultSet {
                get { return _defaultSet; }
                set {
                    if (Equals(value, _defaultSet)) return;
                    _defaultSet = value;
                    OnPropertyChanged();
                }
            }

            private TyresEntry _front;

            [NotNull]
            public TyresEntry Front {
                get { return _front; }
                set {
                    if (Equals(value, _front)) return;
                    _front = value;
                    OnPropertyChanged();
                    UpdateDifferentTyres();
                }
            }

            private TyresEntry _rear;

            [NotNull]
            public TyresEntry Rear {
                get { return _rear; }
                set {
                    if (Equals(value, _rear)) return;
                    _rear = value;
                    OnPropertyChanged();
                    UpdateDifferentTyres();
                }
            }

            #region Unique stuff
            private sealed class FrontRearEqualityComparer : IEqualityComparer<TyresSet> {
                public bool Equals(TyresSet x, TyresSet y) {
                    if (ReferenceEquals(x, y)) return true;
                    if (ReferenceEquals(x, null)) return false;
                    if (ReferenceEquals(y, null)) return false;
                    if (x.GetType() != y.GetType()) return false;
                    return TyresEntry.TyresEntryComparer.Equals(x._front, y._front) && TyresEntry.TyresEntryComparer.Equals(x._rear, y._rear);
                }

                public int GetHashCode(TyresSet obj) {
                    unchecked {
                        return ((obj._front != null ? TyresEntry.TyresEntryComparer.GetHashCode(obj._front) : 0) * 397) ^
                                (obj._rear != null ? TyresEntry.TyresEntryComparer.GetHashCode(obj._rear) : 0);
                    }
                }
            }

            public static IEqualityComparer<TyresSet> TyresSetComparer { get; } = new FrontRearEqualityComparer();
            #endregion

            private bool _isDeleted;

            public bool IsDeleted {
                get { return _isDeleted; }
                set {
                    if (Equals(value, _isDeleted)) return;
                    _isDeleted = value;
                    OnPropertyChanged();
                }
            }

            private bool _canBeDeleted;

            public bool CanBeDeleted {
                get { return _canBeDeleted; }
                set {
                    if (Equals(value, _canBeDeleted)) return;
                    _canBeDeleted = value;
                    OnPropertyChanged();
                    _deleteCommand?.RaiseCanExecuteChanged();
                }
            }

            private DelegateCommand _deleteCommand;

            public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
                IsDeleted = true;
            }, () => CanBeDeleted));

            public TyresSet([NotNull] TyresEntry front, [NotNull] TyresEntry rear) {
                Front = front;
                Rear = rear;
            }

            public string GetName() {
                return Front.Name == Rear.Name ? Front.Name : $@"{Front.Name}/{Rear.Name}";
            }

            public string GetShortName() {
                return Front.ShortName == Rear.ShortName ? Front.ShortName : $@"{Front.ShortName}/{Rear.ShortName}";
            }

            public const string DraggableFormat = "X-TyresSet";

            string IDraggable.DraggableFormat => DraggableFormat;
        }

        public sealed class TyresEntry : Displayable, IDraggable {
            [CanBeNull]
            public CarObject Source => _carObjectLazy.Value;

            [NotNull]
            private readonly Lazy<CarObject> _carObjectLazy;

            [NotNull]
            public string SourceId { get; }

            public int Version { get; }

            public IniFileSection MainSection { get; }

            public IniFileSection ThermalSection { get; }

            public bool RearTyres { get; }

            private bool _bothTyres;

            public bool BothTyres {
                get { return _bothTyres; }
                set {
                    if (value == _bothTyres) return;
                    _bothTyres = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayPosition));
                }
            }

            public string DisplayPosition => BothTyres ? "Both tyres" : RearTyres ? "Rear tyres" : "Front tyres";

            public string DisplaySource => $@"{Source?.Name ?? SourceId} ({DisplayPosition.ToLower(CultureInfo.CurrentUICulture)})";

            public string DisplayParams { get; }

            private string _name;

            [NotNull]
            public string Name {
                get { return _name; }
                set {
                    if (string.IsNullOrWhiteSpace(value)) value = @"?";
                    if (value == _name) return;
                    _name = value;
                    OnPropertyChanged();
                    DisplayName = $@"{Name} {DisplayParams}";
                }
            }

            private string _shortName;

            [NotNull]
            public string ShortName {
                get { return _shortName; }
                set {
                    if (Equals(value, _shortName)) return;
                    _shortName = value;
                    OnPropertyChanged();
                }
            }

            [CanBeNull]
            public readonly string WearCurveData;

            [CanBeNull]
            public readonly string PerformanceCurveData;

            [CanBeNull]
            public TyresEntry OtherEntry { get; set; }

            private TyresEntry(string sourceId, int version, IniFileSection mainSection, IniFileSection thermalSection, string wearCurveData,
                    string performanceCurveData, bool rearTyres, Lazy<double?> rimRadiusLazy) {
                SourceId = sourceId;
                _carObjectLazy = new Lazy<CarObject>(() => CarsManager.Instance.GetById(SourceId));

                Version = version;
                MainSection = mainSection;
                ThermalSection = thermalSection;
                RearTyres = rearTyres;

                WearCurveData = wearCurveData;
                PerformanceCurveData = performanceCurveData;

                ReadParameters(rimRadiusLazy);
                DisplayParams = $@"{DisplayWidth}/{DisplayProfile}/R{DisplayRimRadius}";
                Name = mainSection.GetNonEmpty("NAME") ?? @"?";
                ShortName = mainSection.GetNonEmpty("SHORT_NAME") ?? (Name.Length == 0 ? @"?" : Name.Substring(0, 1));
            }

            private double _radius, _rimRadius, _width;

            private void ReadParameters(Lazy<double?> rimRadiusLazy) {
                _radius = MainSection.GetDouble("RADIUS", 0);
                _rimRadius = MainSection.GetDoubleNullable("RIM_RADIUS") ?? rimRadiusLazy.Value ?? -1;
                _width = MainSection.GetDouble("WIDTH", 0);
            }

            public string DisplayWidth => (_width * 1000).Round(OptionRoundNicely ? 1d : 0.1d).ToString(CultureInfo.CurrentUICulture);

            public string DisplayProfile => _rimRadius <= 0 ? @"?" : 
                (100d * (_radius - (_rimRadius + 0.0127)) / _width).Round(OptionRoundNicely ? 5d : 0.5d).ToString(CultureInfo.CurrentUICulture);

            public string DisplayRimRadius => _rimRadius <= 0 ? @"?" : 
                (_rimRadius * 100 / 2.54 * 2 - 1).Round(OptionRoundNicely ? 0.1d : 0.01d).ToString(CultureInfo.CurrentUICulture);

            #region Unique stuff
            public string Footprint => _footprint ?? (_footprint = new IniFile {
                ["main"] = new IniFileSection(MainSection) { ["NAME"] = "", ["SHORT_NAME"] = "", ["WEAR_CURVE"] = "" },
                ["thermal"] = new IniFileSection(ThermalSection) { ["PERFORMANCE_CURVE"] = "" }
            }.ToString());
            private string _footprint;

            private sealed class TyresEntryEqualityComparer : IEqualityComparer<TyresEntry> {
                public bool Equals(TyresEntry x, TyresEntry y) {
                    return ReferenceEquals(x, y) || !ReferenceEquals(x, null) && !ReferenceEquals(y, null) && x.GetType() == y.GetType() &&
                            string.Equals(x.WearCurveData, y.WearCurveData, StringComparison.Ordinal) &&
                            string.Equals(x.PerformanceCurveData, y.PerformanceCurveData, StringComparison.Ordinal) &&
                            string.Equals(x.Footprint, y.Footprint, StringComparison.Ordinal) &&
                            x.Version == y.Version;
                }

                public int GetHashCode(TyresEntry obj) {
                    unchecked {
                        var hashCode = obj.WearCurveData?.GetHashCode() ?? 0;
                        hashCode = (hashCode * 397) ^ (obj.PerformanceCurveData?.GetHashCode() ?? 0);
                        hashCode = (hashCode * 397) ^ (obj.Footprint?.GetHashCode() ?? 0);
                        hashCode = (hashCode * 397) ^ obj.Version;
                        return hashCode;
                    }
                }
            }

            public static IEqualityComparer<TyresEntry> TyresEntryComparer { get; } = new TyresEntryEqualityComparer();
            #endregion

            #region Appropriate levels
            public void SetAppropriateLevel(TyresSet original) {
                var front = GetAppropriateLevel(original.Front);
                AppropriateLevelFront = front.Item1;
                DisplayOffsetFront = front.Item2;

                var rear = GetAppropriateLevel(original.Rear);
                AppropriateLevelRear = rear.Item1;
                DisplayOffsetRear = rear.Item2;
            }

            private static string OffsetOut(double offset) {
                return $@"{(offset >= 0 ? @"+" : @"−")}{(offset * 1000d).Abs().Round()} mm";
            }

            private Tuple<TyresAppropriateLevel, string> GetAppropriateLevel(TyresEntry entry) {
                var radiusOffset = (_radius - entry._radius).Abs() * 1000d;

                var rimRadiusOffset = entry._rimRadius <= 0d ? 0d : (_rimRadius - entry._rimRadius).Abs() * 1000d;
                var widthOffset = (_width - entry._width).Abs() * 1000d;
                var displayOffset = string.Format("• Radius: {0};\n• Rim radius: {1};\n• Width: {2}",
                        OffsetOut(_radius - entry._radius),
                        entry._rimRadius <= 0d ? @"?" : OffsetOut(_rimRadius - entry._rimRadius), 
                        OffsetOut(_width - entry._width));

                if (radiusOffset < 1 && rimRadiusOffset < 1 && widthOffset < 1) {
                    return Tuple.Create(TyresAppropriateLevel.A, displayOffset);
                }

                if (radiusOffset < 3 && rimRadiusOffset < 5 && widthOffset < 12) {
                    return Tuple.Create(TyresAppropriateLevel.B, displayOffset);
                }

                if (radiusOffset < 7 && rimRadiusOffset < 10 && widthOffset < 26) {
                    return Tuple.Create(TyresAppropriateLevel.C, displayOffset);
                }

                if (radiusOffset < 10 && rimRadiusOffset < 16 && widthOffset < 32) {
                    return Tuple.Create(TyresAppropriateLevel.D, displayOffset);
                }

                if (radiusOffset < 15 && rimRadiusOffset < 24 && widthOffset < 50) {
                    return Tuple.Create(TyresAppropriateLevel.E, displayOffset);
                }

                return Tuple.Create(TyresAppropriateLevel.F, displayOffset);
            }

            private TyresAppropriateLevel _appropriateLevelFront;

            public TyresAppropriateLevel AppropriateLevelFront {
                get { return _appropriateLevelFront; }
                set {
                    if (Equals(value, _appropriateLevelFront)) return;
                    _appropriateLevelFront = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayOffsetFrontDescription));
                }
            }

            private string _displayOffsetFront;

            public string DisplayOffsetFront {
                get { return _displayOffsetFront; }
                set {
                    if (Equals(value, _displayOffsetFront)) return;
                    _displayOffsetFront = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayOffsetFrontDescription));
                }
            }

            public string DisplayOffsetFrontDescription => AppropriateLevelFront.GetDescription() + ':' + Environment.NewLine + DisplayOffsetFront;

            private TyresAppropriateLevel _appropriateLevelRear;

            public TyresAppropriateLevel AppropriateLevelRear {
                get { return _appropriateLevelRear; }
                set {
                    if (Equals(value, _appropriateLevelRear)) return;
                    _appropriateLevelRear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayOffsetRearDescription));
                }
            }

            private string _displayOffsetRear;

            public string DisplayOffsetRear {
                get { return _displayOffsetRear; }
                set {
                    if (Equals(value, _displayOffsetRear)) return;
                    _displayOffsetRear = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayOffsetRearDescription));
                }
            }

            public string DisplayOffsetRearDescription => AppropriateLevelRear.GetDescription() + ':' + Environment.NewLine + DisplayOffsetRear;
            #endregion

            #region Draggable
            public const string DraggableFormat = "X-TyresEntry";

            string IDraggable.DraggableFormat => DraggableFormat;
            #endregion

            #region Create
            [CanBeNull]
            public static TyresEntry Create(CarObject car, string id, bool ignoreDamaged) {
                var tyres = car.AcdData?.GetIniFile("tyres.ini");
                if (tyres?.ContainsKey(id) != true) return null;

                var section = tyres[id];
                var thermal = tyres[Regex.Replace(id, @"(?=FRONT|REAR)", @"THERMAL_")];

                var wearCurve = section.GetNonEmpty("WEAR_CURVE");
                var performanceCurve = thermal.GetNonEmpty("PERFORMANCE_CURVE");
                if (ignoreDamaged && (section.GetNonEmpty("NAME") == null || wearCurve == null || performanceCurve == null)) return null;

                var sourceId = section.GetNonEmpty("__CM_SOURCE_ID") ?? car.Id;
                var rear = id.Contains(@"REAR");
                return new TyresEntry(sourceId, tyres["HEADER"].GetInt("VERSION", -1), section, thermal,
                        wearCurve == null ? null : car.AcdData.GetRawFile(wearCurve).Content,
                        performanceCurve == null ? null : car.AcdData.GetRawFile(performanceCurve).Content,
                        rear, GetRimRadiusLazy(tyres, rear));
            }

            private static Lazy<double?> GetRimRadiusLazy(IniFile tyresIni, bool rear) {
                return new Lazy<double?>(() => {
                    return tyresIni.GetSections(rear ? @"REAR" : @"FRONT", -1).Select(x => x.GetDoubleNullable("RIM_RADIUS")).FirstOrDefault(x => x != null);
                });
            }

            [CanBeNull]
            public static TyresEntry CreateFront(CarObject car, int index, bool ignoreDamaged) {
                return Create(car, IniFile.GetSectionNames(@"FRONT", -1).Skip(index).First(), ignoreDamaged);
            }

            [CanBeNull]
            public static TyresEntry CreateRear(CarObject car, int index, bool ignoreDamaged) {
                return Create(car, IniFile.GetSectionNames(@"REAR", -1).Skip(index).First(), ignoreDamaged);
            }
            #endregion
        }

        private class TyresEntryTester : IParentTester<TyresEntry> {
            public static readonly TyresEntryTester Instance = new TyresEntryTester();

            public string ParameterFromKey(string key) {
                return null;
            }

            public bool Test(TyresEntry obj, string key, ITestEntry value) {
                switch (key) {
                    case null:
                        return value.Test(obj.DisplayName);
                    case "n":
                    case "name":
                        return value.Test(obj.Name);
                    case "p":
                    case "params":
                        return value.Test(obj.DisplayParams);
                    case "profile":
                        return value.Test(obj.DisplayProfile);
                    case "radius":
                    case "rimradius":
                        return value.Test(obj.DisplayRimRadius);
                    case "width":
                        return value.Test(obj.DisplayWidth);
                    case "a":
                    case "g":
                    case "grade":
                        return value.Test(obj.AppropriateLevelFront.ToString()) || value.Test(obj.AppropriateLevelRear.ToString());
                    case "fa":
                    case "fg":
                    case "fgrade":
                        return value.Test(obj.AppropriateLevelFront.ToString());
                    case "ra":
                    case "rg":
                    case "rgrade":
                        return value.Test(obj.AppropriateLevelRear.ToString());
                    case "car":
                        return value.Test(obj.SourceId) || value.Test(obj.Source?.DisplayName);
                    case "v":
                    case "ver":
                    case "version":
                        return value.Test(obj.Version);
                }

                return false;
            }

            public bool TestChild(TyresEntry obj, string key, IFilter filter) {
                switch (key) {
                    case "car":
                        return obj.Source != null && filter.Test(CarObjectTester.Instance, obj.Source);
                }
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
            var tyre = e.Data.GetData(TyresEntry.DraggableFormat) as TyresEntry;
            if (tyre == null) return;

            e.Handled = true;

            var set = ((FrameworkElement)sender).DataContext as TyresSet;
            if (set == null) return;

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

        private async void OnRearSlotDrop(object sender, DragEventArgs e) {
            var tyre = e.Data.GetData(TyresEntry.DraggableFormat) as TyresEntry;
            if (tyre == null) return;

            e.Handled = true;

            var set = ((FrameworkElement)sender).DataContext as TyresSet;
            if (set == null) return;

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

        private TyresSet _draggingFrom;
        private bool _draggingFromRearSlot;

        private void OnFrontSlotMouseDown(object sender, MouseButtonEventArgs e) {
            var set = ((FrameworkElement)sender).DataContext as TyresSet;
            if (set == null) return;

            _draggingFrom = set;
            _draggingFromRearSlot = false;
        }

        private void OnRearSlotMouseDown(object sender, MouseButtonEventArgs e) {
            var set = ((FrameworkElement)sender).DataContext as TyresSet;
            if (set == null) return;

            _draggingFrom = set;
            _draggingFromRearSlot = true;
        }
    }

    public enum TyresAppropriateLevel {
        [Description("Full match")]
        A = 0,

        [Description("Almost perfect")]
        B = 1,

        [Description("Good enough")]
        C = 2,

        [Description("Not recommended")]
        D = 3,

        [Description("Not recommended, way off")]
        E = 4,

        [Description("Completely different")]
        F = 5
    }

    public class TyresPlace : Border {
        static TyresPlace() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TyresPlace), new FrameworkPropertyMetadata(typeof(TyresPlace)));
        }

        public static readonly DependencyProperty HighlightColorProperty = DependencyProperty.Register(nameof(HighlightColor), typeof(Brush),
                typeof(TyresPlace));

        public Brush HighlightColor {
            get { return (Brush)GetValue(HighlightColorProperty); }
            set { SetValue(HighlightColorProperty, value); }
        }

        public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(TyresAppropriateLevel),
                typeof(TyresPlace));

        public TyresAppropriateLevel Level {
            get { return (TyresAppropriateLevel)GetValue(LevelProperty); }
            set { SetValue(LevelProperty, value); }
        }
    }
}
