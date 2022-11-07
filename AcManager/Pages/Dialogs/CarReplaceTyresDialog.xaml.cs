using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using AcManager.Controls;
using AcManager.Controls.Presentation;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Tyres;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Tyres;
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
using FirstFloor.ModernUI.Windows.Controls;
using Newtonsoft.Json.Linq;
using StringBasedFilter;
using BooleanSwitch = FirstFloor.ModernUI.Windows.Controls.BooleanSwitch;

namespace AcManager.Pages.Dialogs {
    public partial class CarReplaceTyresDialog {
        private static PluginsRequirement _requirement;
        public static PluginsRequirement Plugins => _requirement ?? (_requirement = new PluginsRequirement(KnownPlugins.Fann));

        private CarReplaceTyresDialog(CarObject target) {
            DataContext = new ViewModel(target);
            InitializeComponent();
            this.OnActualUnload(Model);

            Buttons = new[] {
                CreateExtraDialogButton(AppStrings.Toolbar_Save, new DelegateCommand(() => {
                    if (DataUpdateWarning.Warn(Model.Car)) {
                        Model.SaveCommand.Execute();
                        CloseWithResult(MessageBoxResult.OK);
                    }
                }, () => Model.SaveCommand.CanExecute()).ListenOnWeak(Model.SaveCommand)),
                CreateExtraDialogButton(UiStrings.Cancel, new DelegateCommand(() => {
                    if (!Model.Changed || ShowMessage("Are you sure you want to discard changes?",
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
                set => Apply(value, ref _setsVersion);
            }

            public TyresEntry OriginalTyresFront { get; }

            public TyresEntry OriginalTyresRear { get; }

            private AsyncCommand _changeCarsFilterCommand;

            public AsyncCommand ChangeCarsFilterCommand => _changeCarsFilterCommand ?? (_changeCarsFilterCommand = new AsyncCommand(async () => {
                var newFilter = (await Prompt.ShowAsync("New filter for source cars:", "Source cars filter",
                        SettingsHolder.Content.CarReplaceTyresDonorFilter, "*",
                        suggestions: ValuesStorage.GetStringList("__CarReplaceTyresDonorFilters")))?.Trim();

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

            public BetterObservableCollection<TyresMachinePresetInfo> NeuralMachines { get; }

            public ViewModel(CarObject car) {
                Car = car;
                Tyres = new BetterObservableCollection<TyresEntry>();
                TyresView = new BetterListCollectionView(Tyres);
                Sets = new ChangeableObservableCollection<TyresSet>(car.GetTyresSets());

                var firstSet = Sets.FirstOrDefault();
                if (firstSet == null) {
                    throw new Exception("Can’t detect current tyres params");
                }

                SetsVersion = firstSet.Front.Version;

                _originalTyresSet = car.GetOriginalTyresSet() ?? firstSet;
                OriginalTyresFront = _originalTyresSet.Front;
                OriginalTyresRear = _originalTyresSet.Rear;

                foreach (var set in Sets) {
                    set.Front.SetAppropriateLevel(_originalTyresSet);
                    set.Rear.SetAppropriateLevel(_originalTyresSet);
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

                NeuralMachines = new BetterObservableCollection<TyresMachinePresetInfo>(
                        PresetsManager.Instance.GetSavedPresets(CarCreateTyresMachineDialog.NeuralMachinesCategory).
                                       Select(x => new TyresMachinePresetInfo(x, _originalTyresSet)));
                PresetsManager.Instance.Watcher(CarCreateTyresMachineDialog.NeuralMachinesCategory).Update += OnUpdate;
            }

            private AsyncProgressEntry _tyresLoadingProgress;

            public AsyncProgressEntry TyresLoadingProgress {
                get => _tyresLoadingProgress;
                set => Apply(value, ref _tyresLoadingProgress);
            }

            private bool _tyresLoaded;

            public async Task LoadTyres() {
                if (_tyresLoaded) return;
                _tyresLoaded = true;

                try {
                    Logging.Debug("Loading list of tyres");
                    var tyres = await TyresEntry.GetList(SettingsHolder.Content.CarReplaceTyresDonorFilter,
                            new Progress<AsyncProgressEntry>(p => TyresLoadingProgress = p), default(CancellationToken));
                    Logging.Debug("List loaded: " + tyres?.Count);

                    ActionExtension.InvokeInMainThreadAsync(() => {
                        if (tyres != null) {
                            foreach (var entry in tyres) {
                                entry.SetAppropriateLevel(_originalTyresSet);
                            }
                            Tyres.ReplaceEverythingBy_Direct(tyres);
                        } else {
                            Tyres.Clear();
                        }
                        TyresLoadingProgress = AsyncProgressEntry.Ready;
                    });
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t get a list of tyres", e);
                }
            }

            private void OnUpdate(object sender, EventArgs eventArgs) {
                RefreshNeuralMachines();
            }

            public void RefreshNeuralMachines() {
                NeuralMachines.ReplaceEverythingBy_Direct(PresetsManager.Instance.GetSavedPresets(CarCreateTyresMachineDialog.NeuralMachinesCategory)
                                                                        .Select(x => new TyresMachinePresetInfo(x, _originalTyresSet)));
            }

            private TyresEntry _movingTyres;

            public TyresEntry MovingTyres {
                get => _movingTyres;
                set => Apply(value, ref _movingTyres);
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

            public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand =
                    new DelegateCommand(() => Sets.Save(SetsVersion, Car, OriginalTyresFront, OriginalTyresRear), () => Changed));

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
                if (data.GetData(TyresSet.DraggableFormat) is TyresSet set) {
                    if (Sets.Contains(set)) return null;

                    if (SetsVersion != set.Front.Version) {
                        UpdateVersionLater(set);
                        return null;
                    }

                    set.IsDeleted = false;
                    return set;
                }

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
                await Task.Yield();
                UpdateVersion(tyre);
            }

            private async void UpdateVersionLater(TyresSet tyre) {
                await Task.Yield();
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

            private void UpdateVersion(TyresSet set) {
                if (ShowMessage(
                        $"Existing tyres are v{SetsVersion}, when this pair is v{set.Front.Version}. To add it, app has to remove current tyres first. Are you sure?",
                        "Versions differ", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    SetsVersion = set.Front.Version;

                    set.DefaultSet = true;
                    set.IsDeleted = false;
                    Sets.ReplaceEverythingBy(new[] { set });
                }
            }

            private void UpdateVersion(TyresEntry tyre) {
                if (ShowMessage(
                        $"Existing tyres are v{SetsVersion}, when this pair is v{tyre.Version}. To add it, app has to remove current tyres first. Are you sure?",
                        "Versions differ", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    SetsVersion = tyre.Version;

                    var set = CreateSet(tyre);
                    set.DefaultSet = true;
                    set.IsDeleted = false;
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
                PresetsManager.Instance.Watcher(CarCreateTyresMachineDialog.NeuralMachinesCategory).Update -= OnUpdate;
            }
        }

        public class TyresMachinePresetInfo : TyresMachineExtras {
            private readonly TyresSet _tyres;

            public ISavedPresetEntry Preset { get; }
            public Lazier<byte[]> Data { get; }
            public Lazier<TyresMachine> Machine { get; }
            public Lazier<string> Description { get; }

            private bool _isAppropriate;

            public bool IsAppropriate {
                get => _isAppropriate;
                set => Apply(value, ref _isAppropriate);
            }

            private string _widthRange;

            public string WidthRange {
                get => _widthRange;
                set => Apply(value, ref _widthRange);
            }

            private string _rimRadiusRange;

            public string RimRadiusRange {
                get => _rimRadiusRange;
                set => Apply(value, ref _rimRadiusRange);
            }

            private string _radiusRange;

            public string RadiusRange {
                get => _radiusRange;
                set => Apply(value, ref _radiusRange);
            }

            private string _profileRange;

            public string ProfileRange {
                get => _profileRange;
                set => Apply(value, ref _profileRange);
            }

            private bool _isWidthAppropriate;

            public bool IsWidthAppropriate {
                get => _isWidthAppropriate;
                set => Apply(value, ref _isWidthAppropriate);
            }

            private bool _isRadiusAppropriate;

            public bool IsRadiusAppropriate {
                get => _isRadiusAppropriate;
                set => Apply(value, ref _isRadiusAppropriate);
            }

            private bool _isRimRadiusAppropriate;

            public bool IsRimRadiusAppropriate {
                get => _isRimRadiusAppropriate;
                set => Apply(value, ref _isRimRadiusAppropriate);
            }

            private bool _isProfileAppropriate;

            public bool IsProfileAppropriate {
                get => _isProfileAppropriate;
                set => Apply(value, ref _isProfileAppropriate);
            }

            private TyresSet _fake;

            public TyresSet GetFakeSet(TyresEntry originalFront, TyresEntry originalRear) {
                if (_fake == null) {
                    var front = originalFront.Clone();
                    var rear = originalRear.Clone();
                    front.Name = TyresName ?? @"Unnamed";
                    front.ShortName = TyresShortName ?? @"…";
                    rear.Name = TyresName ?? @"Unnamed";
                    rear.ShortName = TyresShortName ?? @"…";
                    _fake = new TyresSet(front, rear);
                }
                return _fake;
            }

            public TyresMachinePresetInfo(ISavedPresetEntry preset, TyresSet tyres) {
                _tyres = tyres;
                Preset = preset;
                Data = Lazier.Create(preset.ReadBinaryData);
                Machine = Lazier.Create(() => TyresMachine.LoadFrom(Data.RequireValue, this));
                Description = Lazier.CreateAsync(GetDescription);
            }

            private static int _loading;

            private async Task<string> GetDescription() {
                _loading++;
                await Task.Delay(30 * _loading);

                try {
                    return await Task.Run(() => {
                        try {
                            GetMachineBitsQuick(out var w, out var p, out var r, out var t);
                            WidthRange = Range(w.Minimum, w.Maximum, _tyres.Front.Width, _tyres.Rear.Width, v => TyresEntry.GetDisplayWidth(v));
                            RimRadiusRange = Range(r.Minimum - p.Maximum, r.Maximum - p.Minimum, _tyres.Front.RimRadius, _tyres.Rear.RimRadius,
                                    v => TyresEntry.GetDisplayRimRadius(v), "R");
                            RadiusRange = Range(r.Minimum, r.Maximum, _tyres.Front.Radius, _tyres.Rear.Radius, v => TyresEntry.GetDisplayRimRadius(v));
                            ProfileRange = Range(p.Minimum, p.Maximum, _tyres.Front.Radius - _tyres.Front.RimRadius, _tyres.Rear.Radius - _tyres.Front.RimRadius,
                                    v => $@"{v * 100:F1} cm");

                            IsWidthAppropriate = w.Fits(_tyres.Front.Width) && w.Fits(_tyres.Rear.Width);
                            IsRimRadiusAppropriate = p.Fits(_tyres.Front.Radius - _tyres.Front.RimRadius) && p.Fits(_tyres.Rear.Radius - _tyres.Rear.RimRadius);
                            IsProfileAppropriate = p.Fits(_tyres.Front.Radius - _tyres.Front.RimRadius) && p.Fits(_tyres.Rear.Radius - _tyres.Rear.RimRadius);
                            IsRadiusAppropriate = r.Fits(_tyres.Front.Radius) && r.Fits(_tyres.Rear.Radius);
                            IsAppropriate = IsWidthAppropriate && IsRimRadiusAppropriate && IsRadiusAppropriate;

                            var originalTyresName = TyresName;
                            if (TyresName == null) {
                                TyresName = t.GroupBy(x => x.Name).MaxEntryOrDefault(x => x.Count())?.Key;
                                TyresShortName = t.GroupBy(x => x.ShortName).MaxEntryOrDefault(x => x.Count())?.Key;
                            }

                            return originalTyresName ?? GetGenericNames(t);

                            string GetGenericNames(IEnumerable<NeuralTyresSource> array) {
                                return array.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Take(3).Select(x => x.Key).JoinToReadableString();
                            }

                            string Range(double min, double max, double valueFront, double valueRear, Func<double, string> toString, string prefix = null) {
                                var i = valueFront < min || valueRear < min ? $@"[color={_errorColor.Value}]{toString(min)}[/color]" : toString(min);
                                var a = valueFront > max || valueRear > max ? $@"[color={_errorColor.Value}]{toString(max)}[/color]" : toString(max);
                                return $@"{prefix}{i}–{a}";
                            }
                        } catch (Exception e) when (e.ToString().IndexOf(@"FANNCSharp", StringComparison.OrdinalIgnoreCase) != -1) {
                            Logging.Error(e);
                            return "Failed to load: FANN plugin is missing or failed to load".ToSentence();
                        } catch (Exception e) {
                            Logging.Error(e);
                            return $"Failed to load: {e.Message.ToSentenceMember()}".ToSentence();
                        }
                    });
                } finally {
                    _loading--;
                }

                void GetMachineBitsQuick(out TyresMachine.Normalization w, out TyresMachine.Normalization p, out TyresMachine.Normalization r,
                        out IReadOnlyList<NeuralTyresSource> t) {
                    if (Machine.IsSet || Machine.CheckIfIsSetting()) {
                        var m = Machine.RequireValue;
                        w = m.GetNormalization(NeuralTyresOptions.InputWidth);
                        p = m.GetNormalization(NeuralTyresOptions.InputProfile);
                        r = m.GetNormalization(NeuralTyresOptions.InputRadius);
                        t = m.Sources;
                        return;
                    }

                    // Quite hacky approach, to skip loading actual neural networks — which might be
                    // quite massive — before they actually needed.

                    // Notice how I’ve just made that NeuralTyres library and already use duck tape
                    // and all sorts to create walkarounds.

                    // But I think it’s relatively nice, and this way, TyresMachine itself is quite
                    // simplistic and nice. Why bother adding tons of options and states when a simple
                    // controlled exception will do?

                    try {
                        _terminateLoading = true;
                        TyresMachine.LoadFrom(Data.RequireValue, this);
                    } catch (NotSupportedException) { } finally {
                        _terminateLoading = false;
                    }

                    w = _w;
                    p = _p;
                    r = _r;
                    t = _t;
                }
            }

            private bool _terminateLoading;
            private TyresMachine.Normalization _w, _p, _r;
            private IReadOnlyList<NeuralTyresSource> _t;

            public override void OnLoad(ZipArchive archive, JObject manifest, TyresMachine machine) {
                base.OnLoad(archive, manifest, machine);
                if (_terminateLoading) {
                    _w = machine.GetNormalization(NeuralTyresOptions.InputWidth);
                    _p = machine.GetNormalization(NeuralTyresOptions.InputProfile);
                    _r = machine.GetNormalization(NeuralTyresOptions.InputRadius);
                    _t = machine.Sources;
                    throw new NotSupportedException();
                }
            }

            private readonly Lazier<string> _errorColor = Lazier.Create(() => ((Color)Application.Current.Resources["ErrorColor"]).ToHexString());
        }

        public static Task<bool> RunAsync(CarObject target) {
            try {
                var dialog = new CarReplaceTyresDialog(target);
                dialog.ShowDialog();
                return Task.FromResult(dialog.IsResultOk);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t replace tyres", e);
                return Task.FromResult(false);
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

        private Animator _animator;

        private void SelectedMachineChanged(object sender, SelectionChangedEventArgs args) {
            var selected = args.AddedItems.OfType<TyresMachinePresetInfo>().FirstOrDefault();
            if (selected == null) return;

            var originalFront = Model.OriginalTyresFront;
            var originalRear = Model.OriginalTyresRear;

            TyresSet result = null;
            var resultSet = false;
            _animator?.Spawn(
                    () => selected.GetFakeSet(originalFront, originalRear),
                    () => resultSet ? (result ?? throw new NotSupportedException()) : null);

            GenerateNewSet(selected, t => {
                result = t;
                resultSet = true;
            });
        }

        private void GenerateNewSet(TyresMachinePresetInfo selected, Action<TyresSet> callback) {
            var originalFront = Model.OriginalTyresFront;
            var originalRear = Model.OriginalTyresRear;

            Task.Run(() => {
                try {
                    callback(selected.IsAppropriate
                            ? selected.Machine.RequireValue.CreateTyresSet(originalFront, originalRear, selected.TyresName, selected.TyresShortName)
                            : null);
                } catch (Exception e) {
                    callback(null);
                    NonfatalError.Notify("Can’t create tyres", e);
                }
            });
        }

        private async void OnNewTyresMachineButtonClick(object sender, RoutedEventArgs e) {
            var machine = await CarCreateTyresMachineDialog.RunAsync(Model.Car);
            if (machine != null) {
                Model.RefreshNeuralMachines();
            }
        }

        private async void OnReplaceModeLoaded(object sender, RoutedEventArgs e) {
            try {
                await Model.LoadTyres();
                ((BooleanSwitch)sender).Value = true;
            } catch {
                // ignored
            }
        }

        private void OnGenerateModeLoaded(object sender, RoutedEventArgs e) {
            _animator = new Animator((FrameworkElement)sender) { Offset = new Point(-40, -5) };
        }

        private class Animator {
            public Point Offset;

            private static bool TakeItEasy => AppAppearanceManager.Instance.SoftwareRenderingMode;

            private readonly FrameworkElement _that;
            private TranslateTransform _translate, _burningTranslate, _scanTranslate;
            private RotateTransform _rotate, _burningRotate;

            public Animator(FrameworkElement that) {
                _that = that;
                _generatedSet = that.RequireChild<FrameworkElement>("GeneratedSet");
                _flamesAnimationParent = that.RequireChild<Panel>("FlamesAnimationParent");
                _testElement = that.RequireChild<FrameworkElement>("TestElement");
                _burningSet = that.RequireChild<FrameworkElement>("BurningSet");
                _scanAnimationPiece = that.RequireChild<Border>("ScanAnimationPiece");
                _generatedFresh1 = that.RequireChild<FrameworkElement>("GeneratedFresh1");
                _generatedFresh2 = that.RequireChild<FrameworkElement>("GeneratedFresh2");
                _burningFresh1 = that.RequireChild<FrameworkElement>("BurningFresh1");
                _burningFresh2 = that.RequireChild<FrameworkElement>("BurningFresh2");
                _burningBlack = that.RequireChild<FrameworkElement>("BurningBlack");
                _generatedPresenter = that.RequireChild<ContentPresenter>("GeneratedPresenter");
                _burningPresenter = that.RequireChild<ContentPresenter>("BurningPresenter");
                _generatedMask = ((VisualBrush)that.RequireChild<FrameworkElement>("GeneratedMaskBorder").OpacityMask)
                        .Visual.RequireChild<FrameworkElement>("GeneratedMask");
                _glowEffect = (DropShadowEffect)that.RequireChild<FrameworkElement>("RoomGlowAnimationPiece").Effect;

                _glowEffect.RenderingBias = TakeItEasy ? RenderingBias.Performance : RenderingBias.Quality;
                _glowEffect.BlurRadius = TakeItEasy ? 0 : 50;

                if (that.IsLoaded) {
                    Run();
                } else {
                    that.Loaded += OnLoaded;
                }
            }

            private void OnLoaded(object sender, RoutedEventArgs args) {
                _that.Loaded -= OnLoaded;
                Run();
            }

            private void OnUnloaded(object sender, RoutedEventArgs args) {
                _that.Unloaded -= OnUnloaded;
                CompositionTargetEx.Rendering -= OnRendering;

                _generatedMask.Width = 0;
                _generatedMask.Height = 120;
                _flamesAnimationParent.Children.Clear();
                _generatedFresh1.Opacity = _generatedFresh2.Opacity = _burningFresh1.Opacity = _burningFresh2.Opacity
                        = _burningSet.Opacity = _burningBlack.Opacity = 0d;
                _generatedPresenter.Content = _burningPresenter.Content = null;
            }

            private readonly FrameworkElement _generatedSet, _testElement, _burningSet, _generatedMask,
                    _generatedFresh1, _generatedFresh2, _burningFresh1, _burningFresh2, _burningBlack;

            private readonly Panel _flamesAnimationParent;
            private readonly Border _scanAnimationPiece;
            private readonly ContentPresenter _generatedPresenter, _burningPresenter;
            private readonly DropShadowEffect _glowEffect;

            private void Run() {
                _size = new Point(_generatedSet.ActualWidth, _generatedSet.ActualHeight);
                _fireCanvasSize = new Point(_flamesAnimationParent.ActualWidth, _flamesAnimationParent.ActualHeight);
                _bounds = new Rect(0, 0, _testElement.ActualWidth, _testElement.ActualHeight);
                _translate = new TranslateTransform();
                _rotate = new RotateTransform();
                _generatedSet.RenderTransform = new TransformGroup { Children = { _rotate, _translate } };

                _burningTranslate = new TranslateTransform();
                _burningRotate = new RotateTransform();
                _burningSet.RenderTransform = new TransformGroup { Children = { _burningRotate, _burningTranslate } };

                _scanTranslate = new TranslateTransform();
                _scanAnimationPiece.RenderTransform = _scanTranslate;

                CompositionTargetEx.Rendering += OnRendering;
                _that.Unloaded += OnUnloaded;
            }

            private readonly TimeSpan _time = TimeSpan.Zero;
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private int _revertStage;
            private bool _running;
            private Func<TyresSet> _dummyInfo, _resultInfo;
            private TyresSet _existing;
            private double _customSpeed, _customSpeedTimer, _generatedTimer = -1;

            private const double ScanSpeed = 500;
            private const double ScanLimit = 395;
            private const double ScanAssignDummyPosition = 80;
            private const double ScanAssignSlowDownPosition = 120;
            private const double ScanAssignResultPosition = 320;
            private const double ScanOpacityLimit = 390;
            private const double ScanSpawnPosition = 360;

            private void OnRendering(object sender, RenderingEventArgs e) {
                if (_time == e.RenderingTime) return;
                var dt = _stopwatch.Elapsed.TotalSeconds.Clamp(0.001, 1);
                _stopwatch.Restart();

                UpdateFlames(dt);

                if (_generatedTimer > -1) {
                    _generatedTimer += dt * 3;

                    SoFresh(_generatedFresh1, _generatedTimer);
                    SoFresh(_generatedFresh2, _generatedTimer - 0.25);

                    void SoFresh(UIElement element, double value) {
                        element.Opacity = (value > 1d ? 2 - value : value).Saturate() * 0.03;
                    }
                }

                if (!_running) {
                    _scanAnimationPiece.Opacity = 0d;
                    _glowEffect.Opacity = 0d;
                    return;
                }

                if (_revertStage == 10) {
                    if (_customSpeed == 0d || (_customSpeedTimer += dt) > 0.1) {
                        _customSpeed = ScanSpeed * MathUtils.Random(0.2, 0.5);
                        _customSpeedTimer = 0d;
                    }

                    var position = Scan(_customSpeed);
                    if (position > ScanAssignResultPosition + 80 || MathUtils.Random() > 0.96) {
                        SetColor(Colors.Crimson);
                        _revertStage = 11;
                    }
                    ResetPhysics();
                    return;
                }

                if (_revertStage > 10) {
                    _revertStage = _revertStage >= 30 ? 9 : _revertStage + 1;
                    ResetPhysics();
                    return;
                }

                if (_revertStage == 9) {
                    if (Scan(-2 * ScanSpeed) == 0) {
                        _revertStage = 0;
                        _running = false;
                        _existing = null;
                    }
                    ResetPhysics();
                    return;
                }

                if (_revertStage == 1) {
                    if (Scan(-2 * ScanSpeed) == 0) {
                        _revertStage = 0;
                        SetColor(Colors.Cyan);
                    }
                    ResetPhysics();
                    return;
                }

                if (_revertStage == 2) {
                    if (Scan(ScanSpeed) > ScanOpacityLimit) {
                        Restart();
                        _revertStage = 0;
                    } else {
                        UpdatePhysics();
                    }
                    return;
                }

                var scanSpeed = _resultInfo == null ? 1d
                        : (1d - (_scanTranslate.X - ScanAssignSlowDownPosition) / (ScanAssignResultPosition - ScanAssignSlowDownPosition)).Saturate();
                var current = Scan(ScanSpeed * scanSpeed);

                if (current > ScanAssignDummyPosition && _dummyInfo != null) {
                    _existing = _dummyInfo();
                    _dummyInfo = null;
                    _generatedPresenter.DataContext = _existing;
                    if (_existing == null) {
                        _revertStage = 10;
                    }
                }

                if (current > ScanAssignSlowDownPosition && _resultInfo != null) {
                    try {
                        var result = _resultInfo();
                        if (result != null) {
                            _resultInfo = null;
                            _existing = result;
                            _generatedPresenter.DataContext = _existing;
                        }
                    } catch (NotSupportedException) {
                        _resultInfo = null;
                        _revertStage = 10;
                    }
                }

                if (current < ScanSpawnPosition) {
                    ResetPhysics();
                } else {
                    UpdatePhysics();
                }

                double Scan(double dx) {
                    var value = (_scanTranslate.X + dt * dx).Clamp(0, ScanLimit);
                    _scanTranslate.X = value;

                    _scanAnimationPiece.Opacity = (1d - (value - ScanSpawnPosition) / (ScanOpacityLimit - ScanSpawnPosition)).Saturate();
                    _glowEffect.Opacity = _scanAnimationPiece.Opacity;

                    if (value < ScanSpawnPosition) {
                        _generatedMask.Width = value;
                        _generatedMask.Height = 120;
                    } else {
                        _generatedMask.Width = 400;
                        _generatedMask.Height = 240;
                    }

                    return value;
                }

                void ResetPhysics() {
                    _stopped = 0;
                    _translate.X = 0d;
                    _translate.Y = 0d;
                    _rotate.Angle = 0d;
                }

                void UpdatePhysics() {
                    for (var i = 0; i < 10; i++) {
                        PhysicsStep(dt / 10);
                    }

                    _translate.X = _position.X - _size.X / 2d + Offset.X;
                    _translate.Y = _position.Y - _size.Y / 2d + Offset.Y;
                    _rotate.Angle = _rotation.ToDegrees();
                }
            }

            private void SetColor(Color color) {
                _glowEffect.Color = color;
                _scanAnimationPiece.Background = new SolidColorBrush(color);
            }

            private bool _fireActive;
            private double _burningAngularSpeed;

            private void Restart() {
                if (_existing != null && _revertStage < 9) {
                    _fireActive = true;
                    _extraSparksSpawn = false;
                    _burningAngularSpeed = _angularVelocity + _rotate.Angle + MathUtils.Random(-10d, 10d);
                    _burningPresenter.DataContext = _existing;
                    _burningSet.Opacity = 1d;
                    _burningBlack.Opacity = 0d;
                    _burningFresh1.Opacity = _generatedFresh1.Opacity;
                    _burningFresh2.Opacity = _generatedFresh2.Opacity;
                    _burningTranslate.X = _translate.X;
                    _burningTranslate.Y = _translate.Y;
                    _burningRotate.Angle = _rotate.Angle;
                    _existing = null;
                }

                _generatedTimer = -1;
                _generatedFresh1.Opacity = 0d;
                _generatedFresh2.Opacity = 0d;

                _position = new Point(_bounds.Width / 2, _size.Y / 2);
                _velocity = new Point();
                _rotation = 0d;
                _angularVelocity = MathUtils.Random() < 0.3 ? MathUtils.Random(0.1, 0.7d).ToRadians() : MathUtils.Random(-1d, -0.2).ToRadians();
                _scanTranslate.X = 0d;
                SetColor(Colors.Cyan);
                _running = true;
                _revertStage = 0;
            }

            public void Spawn(Func<TyresSet> dummyInfo, Func<TyresSet> resultInfo) {
                _dummyInfo = dummyInfo;
                _resultInfo = resultInfo;
                if (_running && _scanTranslate.X < ScanOpacityLimit) {
                    _revertStage = _scanTranslate.X < ScanSpawnPosition ? 1 : 2;
                } else {
                    Restart();
                }
            }

            private abstract class FireParticleBase {
                public bool IsActive;
                protected Point Position, Velocity;
                protected double Rotation, AngularVelocity;
                protected double ScaleValue, ScaleVelocity;
                protected double StartingPosition;

                private FrameworkElement _element;
                private RotateTransform _rotate;
                private ScaleTransform _scale;
                private TranslateTransform _translate;
                protected SolidColorBrush Brush;

                protected virtual double CornerRadius => 4d;
                protected virtual Color Color => Colors.Yellow;

                protected abstract void Initialize(Point canvasSize);
                protected abstract void Move(double dt);
                protected abstract double UpdateState();

                public void Update(double dt, Panel parent, Point canvasSize, Point lowestPoint, Point setVelocity) {
                    if (!IsActive) {
                        IsActive = true;
                        Initialize(canvasSize);
                    }

                    var velocityCoefficient = Math.Max(setVelocity.Y.Abs() - 0.5, 0);
                    if (velocityCoefficient > 0) {
                        var closiness = (1d - (Position.Y - lowestPoint.Y).Abs() / 120).Saturate().SmootherStep();
                        if (closiness > 0) {
                            var toSide = ((Position.X - lowestPoint.X) / 200).Clamp(-1d, 1d);
                            var toSideCoefficient = (1d / (toSide * toSide * toSide)).Clamp(-200, 200);
                            var force = (lowestPoint.X - canvasSize.X / 2d).Sign() * toSide.Sign() > 0 ? 0.5 : 2;
                            Velocity.X += force * closiness * velocityCoefficient * toSideCoefficient * dt;
                        }
                    }

                    Move(dt);
                    AddTo(Velocity, dt, ref Position);
                    Rotation += AngularVelocity * dt;
                    ScaleValue += ScaleVelocity * dt;

                    if (_element == null) {
                        _translate = new TranslateTransform();
                        _rotate = new RotateTransform();
                        _scale = new ScaleTransform();

                        Brush = new SolidColorBrush(Color);
                        _element = new Rectangle {
                            RadiusX = CornerRadius,
                            RadiusY = CornerRadius,
                            Width = 20d,
                            Height = 20d,
                            Fill = Brush,
                            RenderTransformOrigin = new Point(0.5, 0.5),
                            RenderTransform = new TransformGroup {
                                Children = {
                                    _rotate,
                                    _scale,
                                    _translate
                                }
                            }
                        };

                        parent.Children.Add(_element);
                    }

                    _translate.X = Position.X - 10d;
                    _translate.Y = Position.Y - 10d;
                    _rotate.Angle = Rotation.ToDegrees();
                    _scale.ScaleX = _scale.ScaleY = ScaleValue;
                    var value = UpdateState();
                    _element.Opacity = value.Saturate();
                    if (value <= 0d) {
                        IsActive = false;
                    }
                }
            }

            private class FlameParticle : FireParticleBase {
                protected override void Initialize(Point canvasSize) {
                    AngularVelocity = MathUtils.Random(-1d, 1d);
                    ScaleValue = 1;
                    ScaleVelocity = 2;
                    Position = new Point(canvasSize.X * MathUtils.Random(0.05, 0.95), canvasSize.Y + 10);
                    Rotation = MathUtils.Random(Math.PI);
                    StartingPosition = Position.Y;
                    Velocity = new Point();
                }

                protected override void Move(double dt) {
                    Velocity.Y -= 500 * dt;
                }

                protected override double UpdateState() {
                    var passed = ((Position.Y - 120) / (StartingPosition - 120)).Saturate();
                    Brush.Color = ColorExtension.FromHsb(60d * Math.Pow(passed, 1.3), 1d, Math.Pow(passed, 0.7));
                    return passed <= 0 ? -1d : Math.Pow(passed, 0.7);
                }
            }

            private class SparkParticle : FireParticleBase {
                protected override double CornerRadius => 10d;
                protected override Color Color => Colors.Yellow;
                private double _time, _lifeSpan;

                protected override void Initialize(Point canvasSize) {
                    _time = 0d;
                    _lifeSpan = MathUtils.Random(0.8, 2.2);
                    Position = new Point(canvasSize.X * MathUtils.Random(0.05, 0.95), canvasSize.Y + 10);
                    StartingPosition = Position.Y;
                    Velocity = new Point(0, -200);
                }

                protected override void Move(double dt) {
                    _time += dt;
                    Velocity.X += MathUtils.Random(-1e3, 1e3) * dt;
                    Velocity.Y += (MathUtils.Random(-1e3, 1e3) - 100) * dt;
                    AddTo(Velocity, -1.2 * dt, ref Velocity);
                }

                protected override double UpdateState() {
                    if (Position.Y < -10 || _time > _lifeSpan + 0.25) return -1d;
                    ScaleValue = 0.1 * (1 - ((_time - _lifeSpan) * 4).Saturate());
                    return 1d;
                }
            }

            private readonly FireParticleBase[] _flameParticles = ArrayExtension.CreateArrayOfType<FireParticleBase, FlameParticle>(TakeItEasy ? 40 : 120);
            private readonly FireParticleBase[] _sparks = ArrayExtension.CreateArrayOfType<FireParticleBase, SparkParticle>(TakeItEasy ? 10 : 30);
            private bool _extraSparksSpawn;

            private void UpdateFlames(double dt) {
                var parent = _flamesAnimationParent;
                Update(_flameParticles, _fireActive ? 3 : 0);
                Update(_sparks, _fireActive && (_extraSparksSpawn || MathUtils.Random() > 0.95) ? 1 : 0);

                if (_fireActive) {
                    _burningTranslate.Y += 140 * dt;
                    _burningRotate.Angle += _burningAngularSpeed * dt;
                    var value = (_burningBlack.Opacity + 2 * dt).Saturate();
                    _burningBlack.Opacity = value;
                    _burningSet.Opacity = 1 - value * value;
                    _extraSparksSpawn = value >= 0.8;
                    if (value >= 1) {
                        _fireActive = false;
                        _extraSparksSpawn = false;
                    }
                }

                void Update(FireParticleBase[] array, int activatePerFrame) {
                    activatePerFrame = (activatePerFrame * 60 * dt).Ceiling().RoundToInt();
                    for (var i = array.Length - 1; i >= 0; i--) {
                        var activated = array[i].IsActive;
                        if (activated || activatePerFrame-- > 0) {
                            array[i].Update(dt, parent, _fireCanvasSize, _lowestPoint, _velocity);
                        }
                    }
                }
            }

            private Point _position, _velocity, _size, _fireCanvasSize;
            private Rect _bounds;
            private double _rotation, _angularVelocity;
            private double _rotationSin, _rotationCos;

            private void PhysicsStep(double dt) {
                if (_stopped > 200) {
                    if (_generatedTimer == -1 && !_fireActive) {
                        _generatedTimer = 0;
                    }
                    return;
                }

                dt = dt.Clamp(0.001, 1) * 60;
                _velocity.Y += 0.5 * dt;
                AddTo(_velocity, dt, ref _position);
                AddTo(_velocity, -0.005, ref _velocity);
                _rotation += _angularVelocity * dt;
                _rotationSin = Math.Sin(_rotation);
                _rotationCos = Math.Cos(_rotation);
                _angularVelocity -= _angularVelocity * (0.02 * dt).Saturate();
                _lowestPoint = _position;
                var o = new Point();
                var h = 0;
                TestPoint(-0.5, -0.5, ref o, ref h);
                TestPoint(0.5, -0.5, ref o, ref h);
                TestPoint(-0.5, 0.5, ref o, ref h);
                TestPoint(0.5, 0.5, ref o, ref h);
                AddTo(o, h > 0 ? 1d / h : 0, ref _position);

                if (_velocity.X < 0.1 && _velocity.Y < 0.1 && _angularVelocity < 0.1 && _rotation.Abs() < 0.1) {
                    _stopped = Math.Max(_stopped + (h > 0 ? 1 : -1), 0);
                } else {
                    _stopped = 0;
                }
            }

            private Point _lowestPoint;

            private void TestPoint(double x, double y, ref Point o, ref int h) {
                var local = Rotate(_rotationSin, _rotationCos, _size.X * x, _size.Y * y);
                double xW = local.X + _position.X, yW = local.Y + _position.Y;
                if (yW > _lowestPoint.Y) {
                    _lowestPoint.X = xW;
                    _lowestPoint.Y = yW;
                }
                var bound = xW < _bounds.Left ? _bounds.Left : xW > _bounds.Right ? _bounds.Right : 0;
                if (bound != 0) {
                    Resolve(new Point(bound - _position.X, local.Y), new Point(bound - xW, 0), ref o, ref h);
                }
                if (yW > _bounds.Bottom) {
                    Resolve(new Point(local.X, _bounds.Bottom - _position.Y), new Point(0, _bounds.Bottom - yW), ref o, ref h);
                }
            }

            private void Resolve(Point l, Point w, ref Point o, ref int h) {
                h++;
                AddTo(w, 1, ref o);

                var d = Rotate(_angularVelocity * 0.001, 1d, l.X, l.Y);
                var p = -(w.X * (_velocity.X + (d.X - l.X) / 0.001) + w.Y * (_velocity.Y + (d.Y - l.Y) / 0.001)) / (w.X * w.X + w.Y * w.Y);
                AddTo(w, 1.6 * p * l.Y / Length(l), ref _velocity);
                _angularVelocity += Cross(l, w) * p / 3e4;
            }

            private static double Cross(Point a, Point b) => a.X * b.Y - a.Y * b.X;
            private static double Length(Point p) => Math.Sqrt(p.X * p.X + p.Y * p.Y) + 0.00001;
            private static Point Rotate(double s, double c, double x, double y) => new Point(x * c - y * s, x * s + y * c);

            private static void AddTo(Point p, double m, ref Point t) {
                t.X += p.X * m;
                t.Y += p.Y * m;
            }

            private int _stopped;
        }
    }
}