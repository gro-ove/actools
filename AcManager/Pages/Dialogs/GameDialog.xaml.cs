using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls;
using AcManager.Tools;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class GameDialog : IGameUi {
        [Localizable(false)]
        private static readonly GoodShuffle<string> ProgressStyles = GoodShuffle.Get(new[] {
            "RotatingPlaneProgressRingStyle",
            "DoubleBounceProgressRingStyle",
            "WaveProgressRingStyle",
            "WanderingCubesProgressRingStyle",
            "PulseProgressRingStyle",
            "ChasingDotsProgressRingStyle",
            "ThreeBounceProgressRingStyle",
            "CircleProgressRingStyle"
        });

        private ViewModel Model => (ViewModel)DataContext;

        private readonly CancellationTokenSource _cancellationSource;

        public GameDialog() {
            DataContext = new ViewModel();
            InitializeComponent();

            ProgressRing.Style = FindResource(ProgressStyles.Next) as Style;

            _cancellationSource = new CancellationTokenSource();
            CancellationToken = _cancellationSource.Token;

            Buttons = new[] { CancelButton };
        }

        protected override void OnClosing(CancelEventArgs e) {
            base.OnClosing(e);

            if (IsResultCancel) {
                try {
                    _cancellationSource.Cancel();
                } catch (ObjectDisposedException) {}
            }
        }

        public void Dispose() {
            try {
                _cancellationSource.Dispose();
            } catch (ObjectDisposedException) { }
        }

        private Game.StartProperties _properties;

        void IGameUi.Show(Game.StartProperties properties) {
            _properties = properties;

            ShowDialogWithoutBlocking();
            Model.WaitingStatus = AppStrings.Race_Initializing;
        }

        void IGameUi.OnProgress(Game.ProgressState progress) {
            switch (progress) {
                case Game.ProgressState.Preparing:
                    Model.WaitingStatus = AppStrings.Race_Preparing;
                    break;
                case Game.ProgressState.Launching:
                    Model.WaitingStatus = AppStrings.Race_LaunchingGame;
                    break;
                case Game.ProgressState.Waiting:
                    Model.WaitingStatus = AppStrings.Race_Waiting;
                    break;
                case Game.ProgressState.Finishing:
                    Model.WaitingStatus = AppStrings.Race_CleaningUp;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(progress), progress, null);
            }
        }

        private static BaseFinishedData GetFinishedData(Game.StartProperties properties, Game.Result result) {
            var conditions = properties?.GetAdditional<PlaceConditions>();
            var takenPlace = conditions?.GetTakenPlace(result) ?? PlaceConditions.UnremarkablePlace;

            {
                var extra = result.GetExtraByType<Game.ResultExtraDrift>();
                if (extra != null) {
                    return new DriftFinishedData {
                        Points = extra.Points,
                        MaxCombo = extra.MaxCombo,
                        MaxLevel = extra.MaxLevel,
                        TakenPlace = takenPlace
                    };
                }
            }

            {
                var extra = result.GetExtraByType<Game.ResultExtraTimeAttack>();
                if (extra != null) {
                    var bestLapTime = result.Sessions.SelectMany(x => from lap in x.BestLaps where lap.CarNumber == 0 select lap.Time).MinOrDefault();
                    return new TimeAttackFinishedData {
                        Points = extra.Points,
                        Laps = result.Sessions.Sum(x => x.LapsTotalPerCar.FirstOrDefault()),
                        BestLapTime = bestLapTime == TimeSpan.Zero ? (TimeSpan?)null : bestLapTime,
                        TakenPlace = takenPlace
                    };
                }
            }

            {
                var extra = result.GetExtraByType<Game.ResultExtraBestLap>();
                if (extra != null && extra.IsNotCancelled && result.Sessions.Length == 1 && result.Players.Length == 1) {
                    var bestLapTime = result.Sessions.SelectMany(x => from lap in x.BestLaps where lap.CarNumber == 0 select lap.Time).MinOrDefault();

                    var sectorsPerSections = result.Sessions.SelectMany(x => from lap in x.Laps where lap.CarNumber == 0 select lap.SectorsTime).ToList();
                    var theoreticallLapTime = sectorsPerSections.First().Select((x, i) => sectorsPerSections.Select(y => y[i]).Min()).Sum();

                    return new HotlapFinishedData {
                        Laps = result.Sessions.Sum(x => x.LapsTotalPerCar.FirstOrDefault()),
                        BestLapTime = bestLapTime == TimeSpan.Zero ? (TimeSpan?)null : bestLapTime,
                        TheoreticallLapTime = theoreticallLapTime,
                        TakenPlace = takenPlace
                    };
                }
            }

            var isOnline = properties?.ModeProperties is Game.OnlineProperties;
            var playerName = isOnline && SettingsHolder.Drive.DifferentPlayerNameOnline ? SettingsHolder.Drive.PlayerNameOnline : SettingsHolder.Drive.PlayerName;

            var sessionsData = (from session in result.Sessions
                                let takenPlaces = session.GetTakenPlacesPerCar()
                                select new SessionFinishedData(session.Name.ApartFromLast(@" Session")) {
                                    PlayerEntries = (
                                            from player in result.Players
                                            let car = CarsManager.Instance.GetById(player.CarId)
                                            let carSkin = car.GetSkinById(player.CarSkinId)
                                            select new { Player = player, Car = car, CarSkin = carSkin }
                                            ).Select((entry, i) => new SessionFinishedData.PlayerEntry {
                                                Name = i == 0 ? playerName : entry.Player.Name,
                                                IsPlayer = i == 0,
                                                Car = entry.Car,
                                                CarSkin = entry.CarSkin,

                                                TakenPlace = takenPlaces.ElementAtOrDefault(i) + 1,
                                                PrizePlace = takenPlaces.Length > 1,
                                                LapsCount = session.LapsTotalPerCar.ElementAtOrDefault(i),
                                                BestLapTime = session.BestLaps.Where(x => x.CarNumber == i).MinEntryOrDefault(x => x.Time)?.Time,
                                                Total = session.Laps.Where(x => x.CarNumber == i).Select(x => x.Time).Sum()
                                            }).OrderBy(x => x.TakenPlace).ToList()
                                }).ToList();

            return sessionsData.Count == 1 ? (BaseFinishedData)sessionsData.First() :
                    sessionsData.Any() ? new SessionsFinishedData(sessionsData) : null;
        }

        public abstract class BaseFinishedData : NotifyPropertyChanged {
            public abstract string Title { get; }

            public int TakenPlace { get; set; }
        }

        public class SessionFinishedData : BaseFinishedData {
            public class PlayerEntry {
                public string Name { get; set; }

                public bool IsPlayer { get; set; }

                public bool PrizePlace { get; set; }

                public CarObject Car { get; set; }

                public CarSkinObject CarSkin { get; set; }

                public int TakenPlace { get; set; }

                public int LapsCount { get; set; }

                public TimeSpan? BestLapTime { get; set; }

                public TimeSpan? Total { get; set; }
            }

            public SessionFinishedData(string title) {
                Title = title;
            }

            public override string Title { get; }

            public List<PlayerEntry> PlayerEntries { get; set; }
        }

        public class SessionsFinishedData : BaseFinishedData {
            public List<SessionFinishedData> Sessions { get; set; }

            public SessionsFinishedData(List<SessionFinishedData> sessions) {
                Sessions = sessions;
                SelectedSession = sessions.LastOrDefault();
            }

            private SessionFinishedData _selectedSession;

            public SessionFinishedData SelectedSession {
                get { return _selectedSession; }
                set {
                    if (Equals(value, _selectedSession)) return;
                    _selectedSession = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Title));
                }
            }

            public override string Title => SelectedSession?.Title;
        }

        public class DriftFinishedData : BaseFinishedData {
            public override string Title { get; } = ToolsStrings.Session_Drift;

            public int Points { get; set; }

            public int MaxCombo { get; set; }

            public int MaxLevel { get; set; }
        }

        public class TimeAttackFinishedData : BaseFinishedData {
            public override string Title { get; } = ToolsStrings.Session_TimeAttack;

            public int Points { get; set; }

            public int Laps { get; set; }

            public TimeSpan? BestLapTime { get; set; }
        }

        public class HotlapFinishedData : BaseFinishedData {
            public override string Title { get; } = ToolsStrings.Session_Hotlap;

            public int Laps { get; set; }

            public TimeSpan? BestLapTime { get; set; }

            public TimeSpan? TheoreticallLapTime { get; set; }
        }

        void IGameUi.OnResult(Game.Result result, ReplayHelper replayHelper) {
            if (result != null && result.NumberOfSessions == 1 && result.Sessions.Length == 1
                    && result.Sessions[0].Type == Game.SessionType.Practice && SettingsHolder.Drive.SkipPracticeResults ||
                    _properties.BasicProperties == null) {
                Close();
                return;
            }

            Func<string> buttonText = () => replayHelper?.IsReplayRenamed == true ? AppStrings.RaceResult_UnsaveReplay : AppStrings.RaceResult_SaveReplay;

            var saveReplayButton = CreateExtraDialogButton(buttonText(), () => {
                if (replayHelper == null) return;
                replayHelper.IsReplayRenamed = !replayHelper.IsReplayRenamed;
            });
            if (replayHelper == null) {
                saveReplayButton.IsEnabled = false;
            } else {
                replayHelper.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(ReplayHelper.IsReplayRenamed)) {
                        saveReplayButton.Content = buttonText();
                    }
                };
            }

            var tryAgainButton = CreateExtraDialogButton(AppStrings.RaceResult_TryAgain, () => {
                CloseWithResult(MessageBoxResult.None);
                GameWrapper.StartAsync(_properties).Forget();
            });

            if (_properties == null) {
                tryAgainButton.IsEnabled = false;
            }

            Buttons = new[] {
                saveReplayButton,
                tryAgainButton,
                CloseButton
            };

            if (result == null || !result.IsNotCancelled) {
                Model.CurrentState = ViewModel.State.Cancelled;
                Model.ErrorMessage = _properties?.GetAdditional<AcLogHelper.WhatsGoingOn?>()?.GetDescription();
            } else {
                try {
                    Model.CurrentState = ViewModel.State.Finished;
                    Model.FinishedData = GetFinishedData(_properties, result);
                } catch (Exception e) {
                    Logging.Warning("[GameDialog] IGameUi.OnResult(): " + e);

                    Model.CurrentState = ViewModel.State.Error;
                    Model.ErrorMessage = AppStrings.RaceResult_ResultProcessingError;
                    Buttons = new[] { CloseButton };
                }
            }
        }

        void IGameUi.OnError(Exception exception) {
            Model.CurrentState = ViewModel.State.Error;
            Model.ErrorMessage = exception?.Message ?? ToolsStrings.Common_UndefinedError;
            Buttons = new[] { CloseButton };

            var ie = exception as InformativeException;
            if (ie != null) {
                Model.ErrorDescription = ie.SolutionCommentary;
            }
        }

        public CancellationToken CancellationToken { get; }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {
                CurrentState = State.Waiting;
            }

            public enum State {
                [ControlsLocalizedDescription("Common_Waiting")]
                Waiting,

                [ControlsLocalizedDescription("Common_Finished")]
                Finished,

                [ControlsLocalizedDescription("Common_Cancelled")]
                Cancelled,

                [ControlsLocalizedDescription("Common_Error")]
                Error
            }

            private State _currentState;

            public State CurrentState {
                get { return _currentState; }
                set {
                    if (Equals(value, _currentState)) return;
                    _currentState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Title));
                }
            }

            public string Title => FinishedData?.Title ?? CurrentState.GetDescription();

            private string _waitingStatus;

            public string WaitingStatus {
                get { return _waitingStatus; }
                set {
                    if (Equals(value, _waitingStatus)) return;
                    _waitingStatus = value;
                    OnPropertyChanged();
                }
            }

            private string _errorMessage;

            public string ErrorMessage {
                get { return _errorMessage; }
                set {
                    if (Equals(value, _errorMessage)) return;
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }

            private string _errorDescription = AppStrings.Common_MoreInformationInMainLog;

            public string ErrorDescription {
                get { return _errorDescription; }
                set {
                    if (Equals(value, _errorDescription)) return;
                    _errorDescription = value;
                    OnPropertyChanged();
                }
            }

            private BaseFinishedData _finishedData;

            public BaseFinishedData FinishedData {
                get { return _finishedData; }
                set {
                    if (Equals(value, _finishedData)) return;
                    _finishedData = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        private void ScrollViewer_OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
