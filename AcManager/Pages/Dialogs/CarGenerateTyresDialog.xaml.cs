// ReSharper disable RedundantUsingDirective
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Tyres;
using AcTools.DataFile;
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
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class CarGenerateTyresDialog {
        private ViewModel Model => (ViewModel)DataContext;

        public CarGenerateTyresDialog(CarObject target, IReadOnlyList<TyresMachineInfo> machines) {
            DataContext = new ViewModel(target, machines);
            InitializeComponent();
            this.OnActualUnload(Model);
        }

        private class ViewModel : NotifyPropertyChanged, IDisposable {
            public CarObject Car { get; }
            public IReadOnlyList<TyresMachineInfo> Machines { get; }
            public ChangeableObservableCollection<TyresSet> Sets { get; }
            public TyresEntry OriginalTyresFront { get; }
            public TyresEntry OriginalTyresRear { get; }
            private readonly TyresSet _originalTyresSet;

            public ViewModel(CarObject car, IReadOnlyList<TyresMachineInfo> machines) {
                Car = car;
                Machines = machines;

                Sets = new ChangeableObservableCollection<TyresSet>(TyresSet.GetSets(car));
                if (Sets.Count == 0) {
                    throw new Exception("Can’t detect current tyres params");
                }

                SetsVersion = Sets[0].Front.Version;
                _originalTyresSet = TyresSet.GetOriginal(car) ?? Sets[0];
                OriginalTyresFront = _originalTyresSet.Front;
                OriginalTyresRear = _originalTyresSet.Rear;

                Draggable.DragStarted += OnDragStarted;
                Draggable.DragEnded += OnDragEnded;
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

            public void Dispose() {
                Draggable.DragStarted -= OnDragStarted;
                Draggable.DragEnded -= OnDragEnded;
            }
        }

        public static async Task<bool> RunAsync(CarObject target) {
            try {
                ChangeableObservableCollection<TyresMachineInfo> machines;
                using (WaitingDialog.Create(ControlsStrings.Common_Loading)) {
                    machines = await TyresMachineInfo.LoadMachinesAsync();
                }

                var dialog = new CarGenerateTyresDialog(target, machines);
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

        private void OnNewTyresMachineButtonClick(object sender, RoutedEventArgs e) {
            throw new NotImplementedException();
        }
    }
}