// ReSharper disable RedundantUsingDirective

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using SlimDX;
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

#if DEBUG
            Loaded += (sender, args) => AnimateResult();
#else
            TestElement.Visibility = Visibility.Collapsed;
#endif
        }

        private class PseudoPhysics {
            private Point _position, _velocity, _size;
            private Rect _bounds;
            private double _rotation, _angularVelocity;
            private readonly TranslateTransform _translate;
            private readonly RotateTransform _rotate;

            public PseudoPhysics(FrameworkElement set) {
                var parent = (FrameworkElement)set.Parent;
                _size = new Point(set.ActualWidth, set.ActualHeight);
                _bounds = new Rect(_size.X / 2, 0, parent.ActualWidth, parent.ActualHeight + _size.Y / 2);

                _position = new Point(_bounds.Width / 2d, -500);
                _rotation = MathUtils.Random(-35d, 35d).ToRadians();
                _angularVelocity = MathUtils.Random(-0.935d, 0.935d).ToRadians()/60;

                _translate = new TranslateTransform();
                _rotate = new RotateTransform();
                set.RenderTransform = new TransformGroup { Children = { _rotate, _translate } };
            }

            private void PhysicsStep(double dt) {
                dt = dt.Clamp(0.001, 1) * 60;
                _velocity.Y += 0.3 * dt;
                AddTo(_velocity, dt, ref _position);
                AddTo(_velocity, -0.003, ref _velocity);
                _rotation += _angularVelocity * dt;
                _angularVelocity -= _angularVelocity * (0.05 * dt).Saturate();
                double s = Math.Sin(_rotation), c = Math.Cos(_rotation);
                var o = new Point();
                var h = 0;
                TestPoint(s, c, -0.5, -0.5, ref o, ref h);
                TestPoint(s, c, 0.5, -0.5, ref o, ref h);
                TestPoint(s, c, -0.5, 0.5, ref o, ref h);
                TestPoint(s, c, 0.5, 0.5, ref o, ref h);
                AddTo(o, h > 0 ? 1d / h : 0, ref _position);
            }

            private void TestPoint(double s, double c, double x, double y, ref Point o, ref int h) {
                var local = Rotate(_size);
                double xW = local.X + _position.X, yW = local.Y + _position.Y;
                var bound = xW < _bounds.Left ? _bounds.Left : xW > _bounds.Right ? _bounds.Right : 0;
                if (bound != 0) {
                    Resolve(new Point(bound - _position.X, local.Y), new Point(bound - xW, 0), ref o, ref h);
                }
                if (yW > _bounds.Bottom) {
                    Resolve(new Point(local.X, _bounds.Bottom - _position.Y), new Point(0, _bounds.Bottom - yW), ref o, ref h);
                }
                Point Rotate(Point p) => new Point(p.X * x * c - p.Y * y * s, p.X * x * s + p.Y * y * c);
            }

            private void Resolve(Point l, Point w, ref Point o, ref int h) {
                h++;
                AddTo(w, 1, ref o);
                AddTo(w, 1, ref _velocity);
                _angularVelocity += Cross(l, w) / 3e4;
                double Cross(Point a, Point b) => a.X * b.Y - a.Y * b.X;
            }

            private static void AddTo(Point p, double m, ref Point t) {
                t.X += p.X * m;
                t.Y += p.Y * m;
            }

            public void OnTick(double dt) {
                for (var i = 0; i < 10; i++) {
                    PhysicsStep(dt / 10);
                }
                _translate.X = _position.X - _size.X;
                _translate.Y = _position.Y - _size.Y;
                _rotate.Angle = _rotation.ToDegrees();
            }
        }

        private async void AnimateResult() {
            var physics = new PseudoPhysics(GeneratedSet);
            var scanLine = ScanAnimationPiece;
            var scanTranslate = new TranslateTransform();
            ScanAnimationPiece.RenderTransform = scanTranslate;

            var time = TimeSpan.Zero;
            var stopwatch = Stopwatch.StartNew();
            void OnRendering(object sender, RenderingEventArgs e) {
                if (time == e.RenderingTime) return;
                var dt = stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();

                physics.OnTick(dt);

                scanTranslate.X += dt * 200;
                if (scanTranslate.X > 400) {
                    scanTranslate.X = 0;
                }
            }

            await Task.Delay(100);
            CompositionTargetEx.Rendering += OnRendering;
            this.OnActualUnload(() => { CompositionTargetEx.Rendering -= OnRendering; });
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

                Sets = new ChangeableObservableCollection<TyresSet>(car.GetTyresSets());
                if (Sets.Count == 0) {
                    throw new Exception("Can’t detect current tyres params");
                }

                SetsVersion = Sets[0].Front.Version;
                _originalTyresSet = car.GetOriginalTyresSet() ?? Sets[0];
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
                    GeneratedTyres = await Task.Run(() => TyresMachine.LoadFrom(SelectedMachine.Preset.ReadBinaryData())
                                                                      .CreateTyresSet(OriginalTyresFront, OriginalTyresRear));
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t generate tyres", e);
                } finally {
                    IsGenerating = false;
                }
            }, () => SelectedMachine != null));

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
            var machine = await CarCreateTyresMachineDialog.RunAsync(Model.Car);
            if (machine != null) {
                Model.RefreshNeuralMachines();
            }
        }
    }
}