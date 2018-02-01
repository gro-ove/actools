using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using AcManager.Controls.ViewModels;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive_Race : IQuickDriveModeControl {
        public QuickDrive_Race() {
            InitializeComponent();
            // DataContext = new QuickDrive_RaceViewModel();
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
            get => (QuickDriveModeViewModel)DataContext;
            set => DataContext = value;
        }

        public ViewModel ActualModel => (ViewModel)DataContext;

        public class ViewModel : QuickDriveModeViewModel, IHierarchicalItemPreviewProvider, IRaceGridModeViewModel {
            #region Jump start penalties
            public Game.JumpStartPenaltyType[] JumpStartPenaltyTypes { get; } = {
                Game.JumpStartPenaltyType.None,
                Game.JumpStartPenaltyType.Pits,
                Game.JumpStartPenaltyType.DriveThrough
            };

            [ValueConversion(typeof(Game.JumpStartPenaltyType), typeof(string))]
            private class InnerJumpStartPenaltyTypeToStringConverter : IValueConverter {
                public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                    switch (value as Game.JumpStartPenaltyType?) {
                        case Game.JumpStartPenaltyType.None:
                            return ToolsStrings.Common_Disabled;
                        case Game.JumpStartPenaltyType.Pits:
                            return ToolsStrings.JumpStartPenalty_Pits;
                        case Game.JumpStartPenaltyType.DriveThrough:
                            return ToolsStrings.JumpStartPenalty_DriveThrough;
                        default:
                            return null;
                    }
                }

                public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                    throw new NotSupportedException();
                }
            }

            public static IValueConverter JumpStartPenaltyTypeToStringConverter { get; } = new InnerJumpStartPenaltyTypeToStringConverter();

            private Game.JumpStartPenaltyType _jumpStartPenalty;

            public Game.JumpStartPenaltyType JumpStartPenalty {
                get => _jumpStartPenalty;
                set {
                    if (Equals(value, _jumpStartPenalty)) return;
                    _jumpStartPenalty = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Common properties
            private bool _penalties;

            public bool Penalties {
                get => _penalties;
                set {
                    if (value == _penalties) return;
                    _penalties = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public int LapsNumberMaximum => SettingsHolder.Drive.QuickDriveExpandBounds ? 999 : 200;

            public int LapsNumberMaximumLimited => Math.Min(LapsNumberMaximum, 50);

            private int _lapsNumber;

            public int LapsNumber {
                get => _lapsNumber;
                set {
                    if (Equals(value, _lapsNumber)) return;
                    _lapsNumber = value.Clamp(1, LapsNumberMaximum);
                    OnPropertyChanged();
                    SaveLater();
                    CheckIfTrackFits(RaceGridViewModel.PlayerTrack);
                }
            }
            #endregion

            [NotNull]
            public RaceGridViewModel RaceGridViewModel { get; }

            // ReSharper disable UnassignedField.Global
            protected class OldSaveableData {
                public bool? Penalties, AiLevelFixed, AiLevelArrangeRandomly, AiLevelArrangeReverse;
                public int? AiLevel, AiLevelMin, LapsNumber, OpponentsNumber, StartingPosition;
                public Game.JumpStartPenaltyType? JumpStartPenalty;

                [CanBeNull]
                public string GridTypeId, OpponentsCarsFilter;

                [CanBeNull]
                public string[] ManualList;

                public static bool Test(string serialized) {
                    return !serialized.Contains(@"""Version"":");
                }
            }
            // ReSharper restore UnassignedField.Global

            [Localizable(false)]
            protected class SaveableData {
                public int Version => 2;

                public bool? Penalties;
                public Game.JumpStartPenaltyType? JumpStartPenalty;
                public int? LapsNumber;

                [CanBeNull]
                public string RaceGridSerialized;
            }

            protected virtual void Save(SaveableData result) {
                result.Penalties = Penalties;
                result.JumpStartPenalty = JumpStartPenalty;
                result.LapsNumber = LapsNumber;
                result.RaceGridSerialized = RaceGridViewModel.ExportToPresetData();
            }

            protected virtual void Load(OldSaveableData o) {
                Penalties = o.Penalties ?? true;
                JumpStartPenalty = o.JumpStartPenalty ?? Game.JumpStartPenaltyType.None;
                LapsNumber = o.LapsNumber ?? 2;

                try {
                    RaceGridViewModel.LoadingFromOutside = true;
                    switch (o.GridTypeId) {
                        case "same_car":
                            RaceGridViewModel.Mode = BuiltInGridMode.SameCar;
                            RaceGridViewModel.FilterValue = null;
                            break;

                        case "same_group":
                            RaceGridViewModel.Mode = BuiltInGridMode.CandidatesSameGroup;
                            RaceGridViewModel.FilterValue = null;
                            break;

                        case "manual":
                            RaceGridViewModel.Mode = BuiltInGridMode.CandidatesManual;
                            RaceGridViewModel.FilterValue = null;
                            RaceGridViewModel.NonfilteredList.ReplaceEverythingBy(
                                    o.ManualList?.Select(x => CarsManager.Instance.GetById(x)).NonNull().Select(x => new RaceGridEntry(x)) ??
                                            new RaceGridEntry[0]);
                            break;

                        case "filtered_by_":
                            RaceGridViewModel.Mode = BuiltInGridMode.CandidatesFiltered;
                            RaceGridViewModel.FilterValue = o.OpponentsCarsFilter;
                            break;

                        default:
                            RaceGridViewModel.Mode = RaceGridViewModel.Modes.GetByIdOrDefault<IRaceGridMode>(o.GridTypeId) ??
                                    BuiltInGridMode.SameCar;
                            RaceGridViewModel.FilterValue = o.OpponentsCarsFilter;
                            break;
                    }

                    RaceGridViewModel.ShuffleCandidates = true;
                    RaceGridViewModel.AiLevelArrangeRandom = o.AiLevelArrangeRandomly.HasValue ? (o.AiLevelArrangeRandomly.Value ? 1d : 0d) : 0.2;
                    RaceGridViewModel.AiLevelArrangeReverse = o.AiLevelArrangeReverse ?? true;
                    RaceGridViewModel.AiLevel = o.AiLevel ?? 92;
                    RaceGridViewModel.AiLevelMin = o.AiLevelMin ?? 92;
                    RaceGridViewModel.OpponentsNumber = o.OpponentsNumber ?? 3;
                    RaceGridViewModel.StartingPosition = o.StartingPosition ?? 4;
                    RaceGridViewModel.FinishLoading();
                } finally {
                    RaceGridViewModel.LoadingFromOutside = false;
                }
            }

            protected virtual void Load(SaveableData o) {
                Penalties = o.Penalties ?? true;
                JumpStartPenalty = o.JumpStartPenalty ?? Game.JumpStartPenaltyType.None;
                LapsNumber = o.LapsNumber ?? 2;

                if (o.RaceGridSerialized != null) {
                    RaceGridViewModel.ImportFromPresetData(o.RaceGridSerialized);
                } else {
                    Logging.Warning("Race grid data missing");
                    RaceGridViewModel.Reset();
                }
            }

            protected virtual void Reset() {
                Penalties = true;
                JumpStartPenalty = Game.JumpStartPenaltyType.None;
                LapsNumber = 2;
                RaceGridViewModel.Reset();
            }

            /// <summary>
            /// Will be called in constuctor!
            /// </summary>
            protected virtual void InitializeSaveable() {
                Saveable = new SaveHelper<SaveableData>("__QuickDrive_Race", () => {
                    var r = new SaveableData();
                    Save(r);
                    return r;
                }, Load, Reset);
                Saveable.RegisterUpgrade<OldSaveableData>(OldSaveableData.Test, Load);
            }

            /// <summary>
            /// Will be called in constuctor!
            /// </summary>
            protected virtual bool IgnoreStartingPosition => false;

            public ViewModel(bool initialize = true) {
                // ReSharper disable once VirtualMemberCallInConstructor
                RaceGridViewModel = new RaceGridViewModel(IgnoreStartingPosition);
                RaceGridViewModel.Changed += RaceGridViewModel_Changed;

                // ReSharper disable once VirtualMemberCallInContructor
                InitializeSaveable();

                if (initialize) {
                    Saveable.LoadOrReset();
                } else {
                    Saveable.Reset();
                }

                StartingPositionInputConverter = new NumberInputConverter(
                        s => string.Equals(s, AppStrings.Drive_Ordinal_Random, StringComparison.OrdinalIgnoreCase)
                                ? 0 : string.Equals(s, AppStrings.Drive_Ordinal_Last, StringComparison.OrdinalIgnoreCase)
                                        ? RaceGridViewModel.StartingPositionLimit : FlexibleParser.TryParseInt(s),
                        d => GetDisplayPosition(d.RoundToInt().Clamp(0, RaceGridViewModel.StartingPositionLimit), RaceGridViewModel.StartingPositionLimit));
            }

            private void RaceGridViewModel_Changed(object sender, EventArgs e) {
                SaveLater();
            }

            public void Load() {
            }

            public void Unload() {
                RaceGridViewModel.Dispose();
            }

            public override async Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                    Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties,
                    string serializedQuickDrivePreset) {
                var selectedCar = CarsManager.Instance.GetById(basicProperties.CarId ?? "");
                var selectedTrack = TracksManager.Instance.GetLayoutById(basicProperties.TrackId ?? "", basicProperties.TrackConfigurationId);

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
                } catch (Exception e) when (e.IsCanceled()) {
                    return;
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t create race grid", e);
                    return;
                }

                basicProperties.Ballast = RaceGridViewModel.PlayerBallast;
                basicProperties.Restrictor = RaceGridViewModel.PlayerRestrictor;

                await StartAsync(new Game.StartProperties {
                    BasicProperties = basicProperties,
                    AssistsProperties = assistsProperties,
                    ConditionProperties = conditionProperties,
                    TrackProperties = trackProperties,
                    ModeProperties = GetModeProperties(botCars),
                    AdditionalPropertieses = {
                        new QuickDrivePresetProperty(serializedQuickDrivePreset),
                        new CarCustomDataHelper()
                    }
                });
            }

            protected virtual Game.BaseModeProperties GetModeProperties(IEnumerable<Game.AiCar> botCars) {
                return new Game.RaceProperties {
                    AiLevel = RaceGridViewModel.AiLevelFixed ? RaceGridViewModel.AiLevel : 100,
                    Penalties = Penalties,
                    JumpStartPenalty = JumpStartPenalty,
                    StartingPosition = RaceGridViewModel.StartingPositionLimited == 0
                            ? MathUtils.Random(1, RaceGridViewModel.OpponentsNumberLimited + 2) : RaceGridViewModel.StartingPositionLimited,
                    RaceLaps = LapsNumber,
                    BotCars = botCars
                };
            }

            public override void CheckIfTrackFits(TrackObjectBase track) {
                TrackDoesNotFit = LapsNumber == 1 ? null : TagRequired("circuit", track);
            }

            public override void OnSelectedUpdated(CarObject selectedCar, TrackObjectBase selectedTrack) {
                base.OnSelectedUpdated(selectedCar, selectedTrack);
                RaceGridViewModel.PlayerCar = selectedCar;
                RaceGridViewModel.PlayerTrack = selectedTrack;
            }

            public object GetPreview(object item) {
                if (!(item is ISavedPresetEntry preset)) return null;

                RaceGridViewModel.SaveableData saved;
                try {
                    var data = preset.ReadData();
                    saved = JsonConvert.DeserializeObject<RaceGridViewModel.SaveableData>(data);
                } catch (Exception) {
                    return null;
                }

                var mode = RaceGridViewModel.Modes.GetByIdOrDefault<IRaceGridMode>(saved.ModeId);
                if (mode == null) return null;

                var displayMode = mode.CandidatesMode ? $"{mode.DisplayName} ({ToolsStrings.Drive_GridArrangeWay_Random})" : mode.DisplayName;
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

            public void SetRaceGridData(string serializedRaceGrid) {
                RaceGridViewModel.ImportFromPresetData(serializedRaceGrid);
            }

            public NumberInputConverter StartingPositionInputConverter { get; }
        }

        public static string GetDisplayPosition(int startingPosition, int limit) {
            return startingPosition == 0 ? AppStrings.Drive_Ordinal_Random
                    : startingPosition == limit ? AppStrings.Drive_Ordinal_Last : startingPosition.ToOrdinalShort(AppStrings.Drive_Ordinal_Parameter);
        }

        private class InnerStartingPositionConverter : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                return values.Length != 2 ? null : GetDisplayPosition(values[0].As<int>(), values[1].As<int>());
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                var s = value?.ToString();
                return new object[] { string.Equals(s, AppStrings.Drive_Ordinal_Random, StringComparison.OrdinalIgnoreCase)
                        ? 0 : string.Equals(s, AppStrings.Drive_Ordinal_Last, StringComparison.OrdinalIgnoreCase)
                                ? 99999 : FlexibleParser.TryParseInt(s) ?? 0, 0 };
            }
        }

        public static readonly IMultiValueConverter StartingPositionConverter = new InnerStartingPositionConverter();
    }
}
