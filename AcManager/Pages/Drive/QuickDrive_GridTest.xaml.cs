using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
using AcManager.UserControls;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
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

            protected class SaveableData {
                public bool? Penalties, AiLevelFixed, AiLevelArrangeRandomly, JumpStartPenalty, AiLevelArrangeReverse;
                public int? AiLevel, AiLevelMin, LapsNumber, OpponentsNumber, StartingPosition;
                public string GridTypeId, OpponentsCarsFilter;
                public string[] ManualList;
            }

            protected virtual void Save(SaveableData result) {}

            protected virtual void Load(SaveableData o) {}

            protected virtual void Reset() {
                RaceGridViewModel.OpponentsNumber = 7;
            }

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

            public override void OnSelectedUpdated(CarObject selectedCar, TrackObjectBase selectedTrack) {
                RaceGridViewModel.SetPlayerCar(selectedCar);
                RaceGridViewModel.SetTrack(selectedTrack);
            }
        }
        
        private class InnerStartingPositionConverter : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                if (values.Length != 2) return null;

                var startingPosition = values[0].AsInt();
                var last = values[1].AsInt() == startingPosition;
                return startingPosition == 0 ? AppStrings.Drive_Ordinal_Random
                        : last ? AppStrings.Drive_Ordinal_Last : startingPosition.ToOrdinalShort(AppStrings.Drive_Ordinal_Parameter);
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                return new object[] {
                    FlexibleParser.TryParseInt(value?.ToString()) ?? 0,
                    0
                };
            }
        }

        public static IMultiValueConverter StartingPositionConverter = new InnerStartingPositionConverter();
    }
}
