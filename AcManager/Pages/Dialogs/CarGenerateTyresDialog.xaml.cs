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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

            _animator = new Animator(this) { Offset = new Point(-40, -5) };
        }

        private Animator _animator;

        private void SelectedMachineChanged(object sender, SelectionChangedEventArgs e) {
            // Model.SelectedMachine = e.AddedItems.OfType<TyresMachinePresetInfo>().FirstOrDefault();
            var selected = e.AddedItems.OfType<TyresMachinePresetInfo>().FirstOrDefault();
            if (selected == null) return;

            Task<TyresSet>[] t = { null };
            _animator.Spawn(() => t[0]?.Result);

            var originalFront = Model.OriginalTyresFront;
            var originalRear = Model.OriginalTyresRear;

            t[0] = Task.Run(() => {
                try {
                    return selected.Machine.RequireValue.CreateTyresSet(originalFront, originalRear);
                } catch (Exception ex) {
                    Logging.Warning(ex);
                    Toast.Show("Can’t create tyres", "Something went wrong", () => NonfatalError.Notify("Can’t create tyres", ex));
                    return null;
                }
            });
        }

        public class TyresMachinePresetInfo : NotifyPropertyChanged, ITyresMachineExtras {
            public ISavedPresetEntry Preset { get; }
            public Lazier<byte[]> Data { get; }
            public Lazier<TyresMachine> Machine { get; }
            public Lazier<string> Description { get; }

            private object _icon;

            public object Icon {
                get => _icon;
                private set {
                    if (Equals(value, _icon)) return;
                    _icon = value;
                    OnPropertyChanged();
                }
            }

            public TyresMachinePresetInfo(ISavedPresetEntry preset) {
                Preset = preset;
                Data = Lazier.Create(preset.ReadBinaryData);
                Machine = Lazier.Create(() => TyresMachine.LoadFrom(Data.RequireValue, this));
                Description = Lazier.CreateAsync(async () => {
                    return await Task.Run(() => {
                        var machine = Machine.RequireValue;
                        return $"Tyres version: {machine.TyresVersion}\n"
                                + $"Types: {GetGenericNames(machine.Sources)}\n"
                                + $"Training runs: {machine.Options.TrainingRuns}\n"
                                + $"Sources: {machine.Sources.Count}";

                        string GetGenericNames(IEnumerable<NeuralTyresSource> array) {
                            return array.GroupBy(x => x.Name).OrderByDescending(x => x.Count()).Select(x => x.Key).JoinToReadableString();
                        }
                    });
                });
            }

            public void OnSave(ZipArchive archive, JObject manifest) {}

            public void OnLoad(ZipArchive archive, JObject manifest) {
                var icon = manifest.GetStringValueOnly("icon");
                if (icon != null) {
                    ActionExtension.InvokeInMainThreadAsync(() => {
                        Icon = ContentUtils.GetIcon(icon, archive.ReadBytes);
                    });
                }
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