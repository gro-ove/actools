using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using Newtonsoft.Json;
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

        public class ViewModel : QuickDriveModeViewModel, IHierarchicalItemPreviewProvider {
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
                LapsNumber = 2;
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

            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties) {
                var selectedCar = CarsManager.Instance.GetById(basicProperties.CarId);
                var selectedTrack = TracksManager.Instance.GetLayoutById(basicProperties.TrackId, basicProperties.TrackConfigurationId);

                IEnumerable<Game.AiCar> botCars;

                try {
                    using (var waiting = new WaitingDialog()) {
                        if (selectedCar == null || !selectedCar.Enabled) {
                            ModernDialog.ShowMessage(AppStrings.Drive_CannotStart_SelectNonDisabled, AppStrings.Drive_CannotStart_Title, MessageBoxButton.OK);
                            return;
                        }

                        if (selectedTrack == null) {
                            ModernDialog.ShowMessage(AppStrings.Drive_CannotStart_SelectTrack, AppStrings.Drive_CannotStart_Title, MessageBoxButton.OK);
                            return;
                        }

                        botCars = await RaceGridViewModel.GenerateGameEntries(waiting.CancellationToken);
                        if (waiting.CancellationToken.IsCancellationRequested) return;

                        if (botCars == null || !botCars.Any()) {
                            ModernDialog.ShowMessage(AppStrings.Drive_CannotStart_SetOpponent, AppStrings.Drive_CannotStart_Title, MessageBoxButton.OK);
                            return;
                        }
                    }
                } catch (TaskCanceledException) {
                    return;
                }

                await StartAsync(new Game.StartProperties {
                    BasicProperties = basicProperties,
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = GetModeProperties(botCars)
                });
            }

            protected virtual Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.RaceProperties {
                    AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                    Penalties = Penalties,
                    JumpStartPenalty = JumpStartPenalty,
                    StartingPosition = RaceGridViewModel.StartingPosition == 0
                            ? MathUtils.Random(1, RaceGridViewModel.OpponentsNumber + 2) : RaceGridViewModel.StartingPosition,
                    RaceLaps = LapsNumber,
                    BotCars = botCars
                };
            }

            public override void OnSelectedUpdated(CarObject selectedCar, TrackObjectBase selectedTrack) {
                RaceGridViewModel.PlayerCar = selectedCar;
                RaceGridViewModel.PlayerTrack = selectedTrack;
            }

            public object GetPreview(object item) {
                var preset = item as ISavedPresetEntry;
                if (preset == null) return null;

                RaceGridViewModel.SaveableData saved;
                try {
                    var data = preset.ReadData();
                    saved = JsonConvert.DeserializeObject<RaceGridViewModel.SaveableData>(data);
                } catch (Exception) {
                    return null;
                }

                var mode = RaceGridViewModel.Modes.HierarchicalGetByIdOrDefault<IRaceGridMode>(saved.ModeId);
                if (mode == null) return null;

                var displayMode = mode.CandidatesMode ? $"{mode.DisplayName} ({"Random"})" : mode.DisplayName;
                var opponentsNumber = mode.CandidatesMode ? saved.OpponentsNumber : saved.CarIds?.Length;
                var description = new[] {
                    $"Mode: [b]{displayMode}[/b]",
                    mode == BuiltInGridMode.Custom
                            ? $"Opponents: [b]{(saved.CarIds?.Length ?? saved.OpponentsNumber ?? 0).ToInvariantString() ?? @"?"}[/b]" : null,
                    mode == BuiltInGridMode.CandidatesManual ? $"Candidates: [b]{saved.CarIds?.Length.ToInvariantString() ?? @"?"}[/b]" : null,
                    !string.IsNullOrWhiteSpace(saved.FilterValue) ? $"Filter: [b]“{saved.FilterValue}”[/b]" : null,
                    saved.StartingPosition.HasValue && opponentsNumber.HasValue
                            ? $"Starting position: [b]{GetDisplayPosition(saved.StartingPosition.Value, opponentsNumber.Value)}[/b]" : null,
                }.NonNull().JoinToString(Environment.NewLine);
                return new BbCodeBlock { BbCode = description };
            }
        }

        public static string GetDisplayPosition(int startingPosition, int limit) {
            return startingPosition == 0 ? AppStrings.Drive_Ordinal_Random
                    : startingPosition == limit ? AppStrings.Drive_Ordinal_Last : startingPosition.ToOrdinalShort(AppStrings.Drive_Ordinal_Parameter);
        }
        
        private class InnerStartingPositionConverter : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                return values.Length != 2 ? null : GetDisplayPosition(values[0].AsInt(), values[1].AsInt());
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                return new object[] { FlexibleParser.TryParseInt(value?.ToString()) ?? 0, 0 };
            }
        }

        public static IMultiValueConverter StartingPositionConverter = new InnerStartingPositionConverter();
    }
}
