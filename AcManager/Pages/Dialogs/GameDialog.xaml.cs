using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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

        private GameDialogViewModel Model => (GameDialogViewModel)DataContext;

        public GameDialog() {
            DataContext = new GameDialogViewModel();
            InitializeComponent();

            ProgressRing.Style = FindResource(ProgressStyles.Next) as Style;

            _cancellationSource = new CancellationTokenSource();
            CancellationToken = _cancellationSource.Token;

            Buttons = new Button[] { };
        }

        protected override void OnClosing(CancelEventArgs e) {
            base.OnClosing(e);

            if (IsResultCancel) {
                _cancellationSource.Cancel();
            }
        }

        public void Dispose() {
            _cancellationSource.Dispose();
        }

        private Game.StartProperties _properties;

        void IGameUi.Show(Game.StartProperties properties) {
            _properties = properties;

            Logging.Write("[GAMEDIALOG] Show()");

            ShowDialogWithoutBlocking();
            Model.WaitingStatus = @"Initializing…";
        }

        void IGameUi.OnProgress(Game.ProgressState progress) {
            switch (progress) {
                case Game.ProgressState.Preparing:
                    Model.WaitingStatus = @"Preparing…";
                    break;
                case Game.ProgressState.Launching:
                    Model.WaitingStatus = @"Launching game…";
                    break;
                case Game.ProgressState.Waiting:
                    Model.WaitingStatus = @"Waiting for the end of race…";
                    break;
                case Game.ProgressState.Finishing:
                    Model.WaitingStatus = @"Cleaning up some stuff…";
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
                                select new SessionFinishedData(session.Name.ApartFromLast(" Session")) {
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
            public override string Title { get; } = "Drift";

            public int Points { get; set; }

            public int MaxCombo { get; set; }

            public int MaxLevel { get; set; }
        }

        public class TimeAttackFinishedData : BaseFinishedData {
            public override string Title { get; } = "Time Attack";

            public int Points { get; set; }

            public int Laps { get; set; }

            public TimeSpan? BestLapTime { get; set; }
        }

        public class HotlapFinishedData : BaseFinishedData {
            public override string Title { get; } = "Hotlap";

            public int Laps { get; set; }

            public TimeSpan? BestLapTime { get; set; }

            public TimeSpan? TheoreticallLapTime { get; set; }
        }

        void IGameUi.OnResult(Game.Result result) {
            if (result != null && result.NumberOfSessions == 1 && result.Sessions.Length == 1
                    && result.Sessions[0].Type == Game.SessionType.Practice && SettingsHolder.Drive.SkipPracticeResults) {
                Close();
                return;
            }

            var tryAgainButton = CreateExtraDialogButton(@"Try again", () => {
                CloseWithResult(MessageBoxResult.Cancel);
                GameWrapper.StartAsync(_properties).Forget();
            });

            if (_properties == null) {
                tryAgainButton.IsEnabled = false;
            }

            Buttons = new[] {
                tryAgainButton,
                CloseButton
            };

            if (result == null || !result.IsNotCancelled) {
                Model.CurrentState = GameDialogViewModel.State.Cancelled;
                Model.ErrorMessage = _properties?.GetAdditional<AcLogHelper.WhatsGoingOn?>()?.GetDescription();
            } else {
                try {
                    Model.CurrentState = GameDialogViewModel.State.Finished;
                    Model.FinishedData = GetFinishedData(_properties, result);
                } catch (Exception e) {
                    Logging.Warning("[GAMEDIALOG] Exception: " + e);

                    Model.CurrentState = GameDialogViewModel.State.Error;
                    Model.ErrorMessage = "Result processing error";
                    Buttons = new[] { CloseButton };
                }
            }
        }

        void IGameUi.OnError(Exception exception) {
            Model.CurrentState = GameDialogViewModel.State.Error;
            Model.ErrorMessage = exception?.Message ?? "Undefined error";
            Buttons = new[] { CloseButton };
        }

        private readonly CancellationTokenSource _cancellationSource;

        public CancellationToken CancellationToken { get; }

        public class GameDialogViewModel : NotifyPropertyChanged {
            public GameDialogViewModel() {
                CurrentState = State.Waiting;
            }

            public enum State {
                [Description("Waiting for the result…")]
                Waiting,

                [Description("Finished")]
                Finished,

                [Description("Cancelled")]
                Cancelled,

                [Description("Error")]
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
