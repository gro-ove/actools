using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using StringBasedFilter;
using WaitingDialog = AcManager.Controls.Dialogs.WaitingDialog;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_GridTest : IQuickDriveModeControl {
        public QuickDrive_GridTest() {
            InitializeComponent();
            // DataContext = new QuickDrive_GridTestViewModel();
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            ActualModel.Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            ActualModel.Unload();
        }


        public QuickDriveModeViewModel Model {
            get { return (QuickDriveModeViewModel)DataContext; }
            set { DataContext = value; }
        }

        public ViewModel ActualModel => (ViewModel)DataContext;

        public class ViewModel : QuickDriveModeViewModel {
            public RaceGridViewModel RaceGridViewModel { get; } = new RaceGridViewModel();

            private bool _penalties;

            public bool Penalties {
                get { return _penalties; }
                set {
                    if (value == _penalties) return;
                    _penalties = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _jumpStartPenalty;

            public bool JumpStartPenalty {
                get { return _jumpStartPenalty; }
                set {
                    if (Equals(value, _jumpStartPenalty)) return;
                    _jumpStartPenalty = value;
                    OnPropertyChanged();
                }
            }

            public int LapsNumberMaximum => SettingsHolder.Drive.QuickDriveExpandBounds ? 999 : 40;

            public int LapsNumberMaximumLimited => Math.Min(LapsNumberMaximum, 50);

            private int _lapsNumber;

            public int LapsNumber {
                get { return _lapsNumber; }
                set {
                    if (Equals(value, _lapsNumber)) return;
                    _lapsNumber = value.Clamp(1, LapsNumberMaximum);
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private int _opponentsNumber;
            private int _unclampedOpponentsNumber;
            private int? _unclampedStartingPosition;

            public int OpponentsNumber {
                get { return _opponentsNumber; }
                set {
                    if (Equals(value, _opponentsNumber)) return;

                    _unclampedOpponentsNumber = value;
                    _opponentsNumber = value.Clamp(1, OpponentsNumberLimit);

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StartingPositionLimit));

                    if (_unclampedStartingPosition.HasValue && _unclampedStartingPosition != StartingPosition) {
                        StartingPosition = _unclampedStartingPosition.Value;
                    }

                    if (_last || StartingPosition > StartingPositionLimit) {
                        _innerChange = true;
                        StartingPosition = StartingPositionLimit;
                        _innerChange = false;
                    } else if (StartingPosition == StartingPositionLimit && StartingPositionLimit != 0) {
                        _last = true;
                        OnPropertyChanged(nameof(DisplayStartingPosition));
                    }

                    SaveLater();
                }
            }

            private int _trackPitsNumber;

            public int TrackPitsNumber {
                get { return _trackPitsNumber; }
                set {
                    if (Equals(value, _trackPitsNumber)) return;
                    _trackPitsNumber = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OpponentsNumberLimit));

                    if (_unclampedOpponentsNumber != _opponentsNumber) {
                        OpponentsNumber = _unclampedOpponentsNumber;
                    } else if (OpponentsNumber > OpponentsNumberLimit) {
                        OpponentsNumber = OpponentsNumberLimit;
                    }
                }
            }

            public int OpponentsNumberLimit => TrackPitsNumber - 1;

            private bool _last;
            private int _startingPosition;
            private bool _innerChange;

            public int StartingPosition {
                get { return _startingPosition; }
                set {
                    if (!_innerChange) {
                        _unclampedStartingPosition = Math.Max(value, 0);
                    }

                    value = value.Clamp(0, StartingPositionLimit);
                    if (Equals(value, _startingPosition)) return;

                    _startingPosition = value;
                    _last = value == StartingPositionLimit && StartingPositionLimit != 0;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayStartingPosition));
                    SaveLater();
                }
            }

            public string DisplayStartingPosition {
                get {
                    return StartingPosition == 0 ? AppStrings.Drive_Ordinal_Random
                            : _last ? AppStrings.Drive_Ordinal_Last : StartingPosition.ToOrdinalShort(AppStrings.Drive_Ordinal_Parameter);
                }
                set { StartingPosition = FlexibleParser.TryParseInt(value) ?? StartingPosition; }
            }

            public int StartingPositionLimit => OpponentsNumber + 1;

            protected class SaveableData {
                public bool? Penalties, AiLevelFixed, AiLevelArrangeRandomly, JumpStartPenalty, AiLevelArrangeReverse;
                public int? AiLevel, AiLevelMin, LapsNumber, OpponentsNumber, StartingPosition;
                public string GridTypeId, OpponentsCarsFilter;
                public string[] ManualList;
            }

            protected virtual void Save(SaveableData result) {}

            protected virtual void Load(SaveableData o) {}

            protected virtual void Reset() {}

            /// <summary>
            /// Will be called in constuctor!
            /// </summary>
            protected virtual void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_GridTest", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
            }

            public ViewModel(bool initialize = true) {
                // ReSharper disable once VirtualMemberCallInContructor
                InitializeSaveable();

                if (initialize) {
                    Saveable.LoadOrReset();
                } else {
                    Saveable.Reset();
                }
            }

            public void Load() {
            }

            public void Unload() {
                RaceGridViewModel.Dispose();
            }

            public override Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties) {
                throw new NotImplementedException();
            }

            private TrackObjectBase _selectedTrack;

            public override void OnSelectedUpdated(CarObject selectedCar, TrackObjectBase selectedTrack) {
                RaceGridViewModel.SetPlayerCar(selectedCar);
                RaceGridViewModel.SetTrack(selectedTrack);

                if (_selectedTrack != null) {
                    _selectedTrack.PropertyChanged -= SelectedTrack_PropertyChanged;
                }

                _selectedTrack = selectedTrack;

                if (selectedTrack != null) {
                    TrackPitsNumber = FlexibleParser.ParseInt(selectedTrack.SpecsPitboxes, 2);
                    _selectedTrack.PropertyChanged += SelectedTrack_PropertyChanged;
                }
            }

            void SelectedTrack_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (_selectedTrack != null && e.PropertyName == nameof(_selectedTrack.SpecsPitboxes)) {
                    TrackPitsNumber = FlexibleParser.ParseInt(_selectedTrack.SpecsPitboxes, 2);
                }
            }
        }

        private void OpponentsCarsFilterTextBox_OnLostFocus(object sender, RoutedEventArgs e) {
            // ((ViewModel)Model).AddOpponentsCarsFilter();
        }
    }
}
