using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Effects;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Pages.Selected;
using AcManager.Tools;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using DataGrid = System.Windows.Controls.DataGrid;
using MenuItem = System.Windows.Controls.MenuItem;

namespace AcManager.Pages.Dialogs {
    public partial class GameDialog : IGameUi {
        public static bool OptionBenchmarkReplays = false;
        public static bool OptionHideCancelButton = false;

        public static readonly int DefinitelyNonPrizePlace = 99999;
        private static GoodShuffle<string> _progressStyles;

        private ViewModel Model => (ViewModel)DataContext;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly bool _resultsViewMode;

        public GameDialog() {
            DataContext = new ViewModel();
            InitializeComponent();

            if (_progressStyles == null) {
                _progressStyles = GoodShuffle.Get(ExtraProgressRings.Styles.Keys);
            }

            _cancellationSource = new CancellationTokenSource();
            CancellationToken = _cancellationSource.Token;

            if (!AppAppearanceManager.Instance.SoftwareRenderingMode) {
                ProgressRing.Style = ExtraProgressRings.Styles.GetValueOrDefault(_progressStyles.Next);
            }

            Buttons = new[] { OptionHideCancelButton ? null : CancelButton };
        }

        public GameDialog(Game.Result readyResult) {
            _resultsViewMode = true;
            DataContext = new ViewModel();
            InitializeComponent();
            OnResult(readyResult, null);
        }

        private IDisposable _disabledWindowHandling;

        protected override void OnLoadedOverride() {
            base.OnLoadedOverride();

            if (ProgressRing.Effect is DropShadowEffect effect) {
                effect.BlurRadius *= ProgressRing.DensityMultiplier;
            }

            PrepareToFixSize().Ignore();
        }

        private async Task PrepareToFixSize() {
            await Task.Yield();
            if (_disabledWindowHandling != null) return;

            var video = new IniFile(AcPaths.GetCfgVideoFilename())["VIDEO"];
            var width = video.GetInt("WIDTH", 1920);
            var height = video.GetInt("HEIGHT", 1080);
            var isAcFullscreen = video.GetBool("FULLSCREEN", false);
            if (isAcFullscreen && (Screen.AllScreens.Any(x => x.Bounds.Width > width) || Screen.AllScreens.Any(x => x.Bounds.Height > height))) {
                Logging.Here();
                _disabledWindowHandling = DisableAnyLogic();
            }
        }

        private async Task RevertSizeFix() {
            if (_disabledWindowHandling == null) return;
            await Task.Delay(800);
            Logging.Here();
            DisposeHelper.Dispose(ref _disabledWindowHandling);
        }

        protected override void OnClosingOverride(CancelEventArgs e) {
            if (IsResultCancel) {
                try {
                    _cancellationSource.Cancel();
                } catch (ObjectDisposedException) { }
            }

            base.OnClosingOverride(e);
            RevertSizeFix().Ignore();
        }

        public void Dispose() {
            try {
                _cancellationSource.Dispose();
            } catch (ObjectDisposedException) { }
        }

        [CanBeNull]
        private Game.StartProperties _properties;

        private GameMode _mode;

        public void Show(Game.StartProperties properties, GameMode mode) {
            _properties = properties;
            _mode = mode;

            ShowDialogAsync().Forget();
            Model.WaitingStatus = AppStrings.Race_Initializing;

            if (SettingsHolder.Drive.WatchForSharedMemory
                    && (SettingsHolder.Drive.MonitorFramesPerSecond || mode == GameMode.Benchmark || mode == GameMode.Replay && OptionBenchmarkReplays)) {
                AcSharedMemory.Instance.MonitorFramesPerSecond = true;
            }
        }

        public void OnProgress(string progress) {
            Model.WaitingStatus = progress;
        }

        public void OnProgress(Game.ProgressState progress) {
            switch (progress) {
                case Game.ProgressState.Preparing:
                    Model.WaitingStatus = AppStrings.Race_Preparing;
                    break;
                case Game.ProgressState.Launching:
                    if (SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.DeveloperStarterType) {
                        Model.WaitingStatus = "Now, run AC…";
                    } else {
                        Model.WaitingStatus = _mode == GameMode.Race ? AppStrings.Race_LaunchingGame :
                                _mode == GameMode.Replay ? AppStrings.Race_LaunchingReplay : AppStrings.Race_LaunchingBenchmark;
                    }
                    break;
                case Game.ProgressState.Waiting:
                    Model.WaitingStatus = _mode == GameMode.Race ? AppStrings.Race_Waiting :
                            _mode == GameMode.Replay ? AppStrings.Race_WaitingReplay : AppStrings.Race_WaitingBenchmark;
                    break;
                case Game.ProgressState.Finishing:
                    RevertSizeFix().Ignore();
                    Model.WaitingStatus = AppStrings.Race_CleaningUp;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(progress), progress, null);
            }
        }

        private static BaseFinishedData GetFinishedData(Game.StartProperties properties, Game.Result result) {
            var conditions = properties?.GetAdditional<PlaceConditions>();
            var takenPlace = conditions?.GetTakenPlace(result) ?? PlaceConditions.UnremarkablePlace;

            Logging.Debug($"Place conditions: {conditions?.GetDescription()}, result: {result.GetDescription()}");

            if (result.GetExtraByType<Game.ResultExtraDrift>(out var drift)) {
                return new DriftFinishedData {
                    Points = drift.Points,
                    MaxCombo = drift.MaxCombo,
                    MaxLevel = drift.MaxLevel,
                    TakenPlace = takenPlace
                };
            }

            if (result.GetExtraByType<Game.ResultExtraTimeAttack>(out var timeAttack)) {
                var bestLapTime = result.Sessions?.SelectMany(x => from lap in x.BestLaps where lap.CarNumber == 0 select lap.Time).MinOrDefault();
                return new TimeAttackFinishedData {
                    Points = timeAttack.Points,
                    Laps = result.Sessions?.Sum(x => x.LapsTotalPerCar?.FirstOrDefault() ?? 0) ?? 0,
                    BestLapTime = bestLapTime == TimeSpan.Zero ? null : bestLapTime,
                    TakenPlace = takenPlace
                };
            }

            if (result.GetExtraByType<Game.ResultExtraBestLap>(out var bestLap) && bestLap.IsNotCancelled && result.Sessions?.Length == 1 &&
                    result.Players?.Length == 1) {
                var bestLapTime = result.Sessions.SelectMany(x => from lap in x.BestLaps where lap.CarNumber == 0 select lap.Time).MinOrDefault();

                var sectorsPerSections = result.Sessions.SelectMany(x => from lap in x.Laps where lap.CarNumber == 0 select lap.SectorsTime).ToList();
                var theoreticallLapTime = sectorsPerSections.FirstOrDefault()?.Select((x, i) => sectorsPerSections.Select(y => y[i]).Min()).Sum();

                return result.Sessions[0].Name == AppStrings.Rsr_SessionName ? new RsrFinishedData {
                    Laps = result.Sessions.Sum(x => x.LapsTotalPerCar?.FirstOrDefault() ?? 0),
                    BestLapTime = bestLapTime == TimeSpan.Zero ? (TimeSpan?)null : bestLapTime,
                    TheoreticallLapTime = theoreticallLapTime,
                    TakenPlace = takenPlace
                } : new HotlapFinishedData {
                    Laps = result.Sessions.Sum(x => x.LapsTotalPerCar?.FirstOrDefault() ?? 0),
                    BestLapTime = bestLapTime == TimeSpan.Zero ? (TimeSpan?)null : bestLapTime,
                    TheoreticallLapTime = theoreticallLapTime,
                    TakenPlace = takenPlace
                };
            }

            var isOnline = properties?.ModeProperties is Game.OnlineProperties;
            var playerName = isOnline && SettingsHolder.Drive.DifferentPlayerNameOnline
                    ? SettingsHolder.Drive.PlayerNameOnline : SettingsHolder.Drive.PlayerName;

            if (result.Sessions?.Length == 1 && result.Sessions[0].Name == Game.TrackDaySessionName && result.Players?.Length > 1) {
                var session = result.Sessions[0];
                var playerLaps = session.LapsTotalPerCar?[0] ?? 1;
                session.LapsTotalPerCar = session.LapsTotalPerCar?.Select(x => Math.Min(x, playerLaps)).ToArray();
                session.Laps = session.Laps?.Where(x => x.LapId < playerLaps).ToArray();
                session.BestLaps = session.BestLaps?.Select(x => {
                    if (x.LapId < playerLaps) return x;
                    var best = session.Laps?.Where(y => y.CarNumber == x.CarNumber).MinEntryOrDefault(y => y.Time);
                    if (best == null) return null;
                    return new Game.ResultBestLap {
                        Time = best.Time,
                        LapId = best.LapId,
                        CarNumber = x.CarNumber
                    };
                }).NonNull().ToArray();
            }

            var dragExtra = result.GetExtraByType<Game.ResultExtraDrag>();
            var sessionsData = result.Sessions?.Select(session => {
                int[] takenPlaces;
                SessionFinishedData data;

                if (dragExtra != null) {
                    data = new DragFinishedData {
                        BestReactionTime = dragExtra.ReactionTime,
                        Total = dragExtra.Total,
                        Wins = dragExtra.Wins,
                        Runs = dragExtra.Runs,
                    };

                    var delta = dragExtra.Wins * 2 - dragExtra.Runs;
                    takenPlaces = new[] {
                        delta >= 0 ? 0 : 1,
                        delta <= 0 ? 0 : 1
                    };
                } else {
                    data = new SessionFinishedData(session.Name?.ApartFromLast(@" Session"));
                    takenPlaces = session.GetTakenPlacesPerCar();
                }

                var sessionBestLap = session.BestLaps?.MinEntryOrDefault(x => x.Time);
                var sessionBest = sessionBestLap?.Time;

                data.PlayerEntries = (
                        from player in result.Players
                        let car = CarsManager.Instance.GetById(player.CarId ?? "")
                        let carSkin = car?.GetSkinById(player.CarSkinId ?? "")
                        select new { Player = player, Car = car, CarSkin = carSkin }
                        ).Select((entry, i) => {
                            var bestLapTime = session.BestLaps?.Where(x => x.CarNumber == i).MinEntryOrDefault(x => x.Time)?.Time;
                            var laps = session.Laps?.Where(x => x.CarNumber == i).Select(x => new SessionFinishedData.PlayerLapEntry {
                                LapNumber = x.LapId + 1,
                                Sectors = x.SectorsTime,
                                Cuts = x.Cuts,
                                TyresShortName = x.TyresShortName,
                                Total = x.Time,
                                DeltaToBest = bestLapTime.HasValue ? x.Time - bestLapTime.Value : (TimeSpan?)null,
                                DeltaToSessionBest = sessionBest.HasValue ? x.Time - sessionBest.Value : (TimeSpan?)null,
                            }).ToArray();

                            var lapTimes = laps?.Skip(1).Select(x => x.Total.TotalSeconds).Where(x => x > 10d).ToList();
                            double? progress, spread;
                            if (lapTimes == null || lapTimes.Count < 2) {
                                progress = null;
                                spread = null;
                            } else {
                                var firstHalf = lapTimes.Count / 2;
                                var firstMedian = lapTimes.Take(firstHalf).Median();
                                var secondMedian = lapTimes.Skip(firstHalf).Median();
                                progress = 1d - secondMedian / firstMedian;
                                spread = (lapTimes.Max() - lapTimes.Min()) / bestLapTime?.TotalSeconds;
                            }

                            return new SessionFinishedData.PlayerEntry {
                                Name = i == 0 ? playerName : entry.Player.Name,
                                IsPlayer = i == 0,
                                Index = i,
                                Car = entry.Car,
                                CarSkin = entry.CarSkin,
                                TakenPlace = i < takenPlaces?.Length ? takenPlaces[i] + 1 : DefinitelyNonPrizePlace,
                                PrizePlace = takenPlaces?.Length > 1,
                                LapsCount = session.LapsTotalPerCar?.ArrayElementAtOrDefault(i) ?? 0,
                                BestLapTime = bestLapTime,
                                DeltaToSessionBest = sessionBestLap?.CarNumber == i ? null : bestLapTime - sessionBest,
                                TotalTime = session.Laps?.Where(x => x.CarNumber == i).Select(x => x.SectorsTime.Sum()).Sum(),
                                Laps = laps,
                                LapTimeProgress = progress,
                                LapTimeSpread = spread
                            };
                        }).OrderBy(x => x.TakenPlace).ToList();

                if (data.PlayerEntries.Count > 0) {
                    var maxLaps = data.PlayerEntries.Select(x => x.Laps.Length).Max();
                    var bestTotalTime = data.PlayerEntries.Where(x => x.Laps.Length == maxLaps).MinEntryOrDefault(x => x.TotalTime ?? TimeSpan.MaxValue);
                    foreach (var entry in data.PlayerEntries) {
                        var lapsDelta = maxLaps - entry.Laps.Length;
                        if (bestTotalTime == entry) {
                            entry.TotalTimeDelta = "-";
                        } else if (lapsDelta > 0) {
                            entry.TotalTimeDelta = $"+{lapsDelta} {PluralizingConverter.Pluralize(lapsDelta, "lap")}";
                        } else {
                            var t = entry.TotalTime - bestTotalTime?.TotalTime;
                            var v = t?.ToMillisecondsString();
                            entry.TotalTimeDelta = t == TimeSpan.Zero ? "" : v != null ? $"+{v}" : null;
                        }
                    }
                }

                var bestProgress = (from player in data.PlayerEntries
                                    where player.LapTimeProgress > 0.03
                                    orderby player.LapTimeProgress descending
                                    select player).FirstOrDefault();
                var bestConsistent = (from player in data.PlayerEntries
                                      where player.LapTimeSpread < 0.02
                                      orderby player.LapTimeSpread descending
                                      select player).FirstOrDefault();

                data.RemarkableNotes = new[] {
                    sessionBestLap == null ? null :
                            new SessionFinishedData.RemarkableNote("[b]Best lap[/b] made by ", data.PlayerEntries.GetByIdOrDefault(sessionBestLap.CarNumber),
                                    null),
                    new SessionFinishedData.RemarkableNote("[b]The Best Off-roader Award[/b] goes to ", (from player in data.PlayerEntries
                                                                                                         let cuts =
                                                                                                                 (double)player.Laps.Sum(x => x.Cuts)
                                                                                                                         / player.Laps.Length
                                                                                                         where cuts > 1.5
                                                                                                         orderby cuts descending
                                                                                                         select player).FirstOrDefault(), null),
                    new SessionFinishedData.RemarkableNote("[b]Remarkable progress[/b] shown by ", bestProgress, null),
                    new SessionFinishedData.RemarkableNote("[b]The most consistent[/b] is ", bestConsistent, null),
                }.Where(x => x?.Player != null).ToList();
                return data;
            }).ToList();

            return sessionsData?.Count == 1 ? (BaseFinishedData)sessionsData.First() :
                    sessionsData?.Any() == true ? new SessionsFinishedData(sessionsData) : null;
        }

        public abstract class BaseFinishedData : NotifyPropertyChanged {
            [CanBeNull]
            public abstract string Title { get; }

            public int TakenPlace { get; set; }
        }

        public class SessionFinishedData : BaseFinishedData {
            public class RemarkableNote : NotifyPropertyChanged {
                public string Message { get; }
                public PlayerEntry Player { get; }
                public string IconDataKey { get; }

                public RemarkableNote(string message, PlayerEntry player, string iconDataKey) {
                    Message = message;
                    Player = player;
                    IconDataKey = iconDataKey;
                }
            }

            public class PlayerEntry : NotifyPropertyChanged, IWithId<int> {
                public string Name { get; set; }
                public bool IsPlayer { get; set; }
                public bool PrizePlace { get; set; }
                public CarObject Car { get; set; }
                public CarSkinObject CarSkin { get; set; }
                public int Index { get; set; }
                public int TakenPlace { get; set; }
                public int LapsCount { get; set; }
                public double? LapTimeProgress { get; set; }
                public double? LapTimeSpread { get; set; }
                public TimeSpan? BestLapTime { get; set; }
                public TimeSpan? DeltaToSessionBest { get; set; }
                public TimeSpan? TotalTime { get; set; }
                public string TotalTimeDelta { get; set; }
                public PlayerLapEntry[] Laps { get; set; }

                private DataTable _lapsDataTable;
                public DataTable LapsDataTable => _lapsDataTable ?? (_lapsDataTable = CreateLapTable(Car, Laps));

                private static DataTable CreateLapTable([CanBeNull] CarObject car, PlayerLapEntry[] laps) {
                    var result = new DataTable();

                    if (laps.Length > 0) {
                        result.Columns.Add("Lap");
                        result.Columns.Add("Tyres");

                        for (var i = 0; i < laps[0].Sectors.Length; i++) {
                            result.Columns.Add($"{(i + 1).ToOrdinal("sector")} sector");
                        }

                        result.Columns.Add("Total");
                        result.Columns.Add("Delta");
                        result.Columns.Add("Session delta");
                    }

                    foreach (var lap in laps.Where(x => x.Total > TimeSpan.Zero)) {
                        var items = new object[5 + lap.Sectors.Length];
                        items[0] = lap.LapNumber;

                        var tyresName = car?.AcdData?.GetIniFile("tyres.ini").GetSections("FRONT", -1)
                                            .FirstOrDefault(x => x.GetNonEmpty("SHORT_NAME") == lap.TyresShortName)?.GetNonEmpty("NAME");
                        items[1] = tyresName == null ? lap.TyresShortName : $"{tyresName} ({lap.TyresShortName})";

                        items[items.Length - 3] = lap.Total.ToMillisecondsString();
                        items[items.Length - 2] = lap.DeltaToBest.HasValue
                                ? lap.DeltaToBest == TimeSpan.Zero ? "-" : "+" + lap.DeltaToBest?.ToMillisecondsString() : "";
                        items[items.Length - 1] = lap.DeltaToSessionBest.HasValue
                                ? lap.DeltaToSessionBest == TimeSpan.Zero ? "-" : "+" + lap.DeltaToSessionBest?.ToMillisecondsString() : "";

                        for (var i = 0; i < lap.Sectors.Length; i++) {
                            items[i + 2] = lap.Sectors[i].ToMillisecondsString();
                        }

                        result.Rows.Add(items);
                    }

                    return result;
                }

                public int Id => Index;
            }

            public class PlayerLapEntry : NotifyPropertyChanged {
                public int LapNumber { get; set; }
                public int Cuts { get; set; }
                public string TyresShortName { get; set; }
                public TimeSpan[] Sectors { get; set; }
                public TimeSpan Total { get; set; }
                public TimeSpan? DeltaToBest { get; set; }
                public TimeSpan? DeltaToSessionBest { get; set; }
            }

            public SessionFinishedData([CanBeNull] string title) {
                Title = title;
            }

            public override string Title { get; }

            public List<PlayerEntry> PlayerEntries { get; set; }
            public List<RemarkableNote> RemarkableNotes { get; set; }
        }

        public class SessionsFinishedData : BaseFinishedData {
            public List<SessionFinishedData> Sessions { get; set; }

            public SessionsFinishedData(List<SessionFinishedData> sessions) {
                Sessions = sessions;
                SelectedSession = sessions.LastOrDefault();
            }

            private SessionFinishedData _selectedSession;

            public SessionFinishedData SelectedSession {
                get => _selectedSession;
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

        public class TrackDayFinishedData : HotlapFinishedData {
            public override string Title { get; } = ToolsStrings.Session_TrackDay;
        }

        public class HotlapFinishedData : BaseFinishedData {
            public override string Title { get; } = ToolsStrings.Session_Hotlap;
            public int Laps { get; set; }
            public TimeSpan? BestLapTime { get; set; }
            public TimeSpan? TheoreticallLapTime { get; set; }
        }

        public class RsrFinishedData : HotlapFinishedData {
            public override string Title { get; } = AppStrings.Rsr_SessionName;
        }

        public class DragFinishedData : SessionFinishedData {
            public int Total { get; set; }
            public int Wins { get; set; }
            public int Runs { get; set; }
            public TimeSpan BestReactionTime { get; set; }
            public DragFinishedData() : base(ToolsStrings.Session_Drag) { }
        }

        public void OnResult(Game.Result result, ReplayHelper replayHelper) {
            RevertSizeFix().Ignore();

            var practiceMode = SettingsHolder.Drive.SkipPracticeResults
                    && !_resultsViewMode // If we got here before dialog is opened, user wants to see results anyway
                    && result != null && result.NumberOfSessions == 1 && result.Sessions?.Length == 1
                    && result.Sessions[0].Type == Game.SessionType.Practice;

            if (SettingsHolder.Drive.WatchForSharedMemory) {
                AcSharedMemory.Instance.MonitorFramesPerSecond = false;
            }

            var data = AcSharedMemory.Instance.GetFpsDetails();

            if (SettingsHolder.Drive.MonitorFramesPerSecond) {
                AcSettingsHolder.Video.LastSessionPerformanceData = data;
            }

            if (data != null) {
                if ((_properties?.BenchmarkProperties != null || OptionBenchmarkReplays && _properties?.ReplayProperties != null)
                        && SettingsHolder.Drive.WatchForSharedMemory) {
                    Model.CurrentState = ViewModel.State.BenchmarkResult;
                    Model.BenchmarkResults = $@"• Average FPS: [b]{data.AverageFps:F1}[/b] ([b]{1000 / data.AverageFps:F3}[/b] ms);
• Minimum FPS: {(data.MinimumFps != null ? $"[b]{data.MinimumFps.Value:F1}[/b] ([b]{1000 / data.MinimumFps.Value:F3}[/b] ms)" : "[b]N/A[/b]")};
• Taken: [b]{PluralizingConverter.PluralizeExt(data.SamplesTaken, "{0} sample")}[/b];
• Test time: [b]{TimeSpan.FromSeconds(data.SamplesTaken * AcSharedMemory.MonitorFramesPerSecondSampleFrequency.TotalSeconds).ToMillisecondsString()}[/b].";
                    Model.BenchmarkPassed = data.MinimumFps > 55;
                    Buttons = new[] { CloseButton };
                    return;
                }
            }

            var isNoResultsMode = _properties?.GetAdditional<WhatsGoingOn>() == null
                    && (_properties?.ReplayProperties != null || _properties?.BenchmarkProperties != null);

            if (practiceMode || isNoResultsMode) {
                if (IsLoaded) {
                    Close();
                    Application.Current?.MainWindow?.Activate();
                } else {
                    Model.CurrentState = ViewModel.State.Error;
                    Model.ErrorMessage = "Nothing to display";
                    Buttons = new[] { CloseButton };
                }
                return;
            }

            // Save replay button
            /* Func<string> buttonText = () => replayHelper?.IsReplayRenamed == true ?
                    AppStrings.RaceResult_UnsaveReplay : AppStrings.RaceResult_SaveReplay;

            var saveReplayButton = CreateExtraDialogButton(buttonText(), () => {
                if (replayHelper == null) {
                    Logging.Warning("ReplayHelper=<NULL>");
                    return;
                }

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
            }*/

            // Save replay alt button
            ButtonWithComboBox saveReplayButton;
            if (replayHelper != null && replayHelper.IsAvailable) {
                string ButtonText() => replayHelper.IsKept ? AppStrings.RaceResult_UnsaveReplay : AppStrings.RaceResult_SaveReplay;
                string SaveAsText() => string.Format(replayHelper.IsKept ? "Saved as “{0}”" : "Suggested replay name: “{0}”", replayHelper.Name);

                saveReplayButton = new ButtonWithComboBox {
                    Margin = new Thickness(4, 0, 0, 0),
                    MinHeight = 21,
                    MinWidth = 65,
                    Content = ToolsStrings.Shared_Replay,
                    Command = new AsyncCommand(replayHelper.Play),
                    MenuItems = {
                        replayHelper.IsRenameable
                                ? new MenuItem {
                                    Header = SaveAsText(),
                                    Command = new DelegateCommand(() => {
                                        var newName = Prompt.Show("Save replay as:", "Replay name", replayHelper.Name, "?", required: true);
                                        if (!string.IsNullOrWhiteSpace(newName)) {
                                            replayHelper.Name = newName;
                                        }

                                        replayHelper.IsKept = true;
                                    })
                                }
                                : new MenuItem { Header = "Replay name: " + replayHelper.Name, StaysOpenOnClick = true },
                        new MenuItem { Header = ButtonText(), Command = new DelegateCommand(replayHelper.ToggleKept) },
                        new Separator(),
                        new MenuItem {
                            Header = "Share replay",
                            Command = new AsyncCommand(() => {
                                var car = _properties?.BasicProperties?.CarId == null ? null :
                                        CarsManager.Instance.GetById(_properties.BasicProperties.CarId);
                                var track = _properties?.BasicProperties?.TrackId == null ? null :
                                        TracksManager.Instance.GetById(_properties.BasicProperties.TrackId);
                                return SelectedReplayPage.ShareReplay(replayHelper.Name, replayHelper.Filename, car, track, false);
                            })
                        },
                    }
                };

                replayHelper.SubscribeWeak((sender, args) => {
                    if (args.PropertyName == nameof(ReplayHelper.IsKept)) {
                        ((MenuItem)saveReplayButton.MenuItems[0]).Header = SaveAsText();
                        ((MenuItem)saveReplayButton.MenuItems[1]).Header = ButtonText();
                    }
                });
            } else {
                saveReplayButton = null;
            }

            var tryAgainButton = CreateExtraDialogButton(AppStrings.RaceResult_TryAgain, () => {
                CloseWithResult(MessageBoxResult.None);
                GameWrapper.StartAsync(_properties).Forget();
            });

            Button fixButton = null;

            if (result == null || !result.IsNotCancelled) {
                Model.CurrentState = ViewModel.State.Cancelled;

                var whatsGoingOn = _properties?.PullAdditional<WhatsGoingOn>();
                var solution = whatsGoingOn?.Solution;
                if (solution != null) {
                    fixButton = this.CreateFixItButton(solution);
                    tryAgainButton = CreateExtraDialogButton($"{solution.DisplayName} and try again", new AsyncCommand(async () => {
                        await solution.ExecuteAsync();
                        CloseWithResult(MessageBoxResult.None);
                        GameWrapper.StartAsync(_properties).Forget();
                    }));
                }

                Model.ErrorMessage = whatsGoingOn?.GetDescription();
            } else {
                try {
                    Model.CurrentState = ViewModel.State.Finished;
                    Model.FinishedData = GetFinishedData(_properties, result);
                } catch (Exception e) {
                    Logging.Warning(e);

                    Model.CurrentState = ViewModel.State.Error;
                    Model.ErrorMessage = AppStrings.RaceResult_ResultProcessingError;
                    Buttons = new[] { CloseButton };
                    return;
                }
            }

            Buttons = new ContentControl[] {
                fixButton,
                fixButton == null ? saveReplayButton : null,
                _properties != null ? tryAgainButton : null,
                CloseButton
            };
        }

        public void OnError(Exception exception) {
            Model.CurrentState = ViewModel.State.Error;
            Model.ErrorMessage = exception?.Message ?? ToolsStrings.Common_UndefinedError;
            Buttons = new[] { CloseButton };

            if (exception is InformativeException ie) {
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
                Error,

                [Description("Benchmark")]
                BenchmarkResult
            }

            private State _currentState;

            public State CurrentState {
                get => _currentState;
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
                get => _waitingStatus;
                set => Apply(value, ref _waitingStatus);
            }

            private string _errorMessage;

            [CanBeNull]
            public string ErrorMessage {
                get => _errorMessage;
                set => Apply(value?.ToSentence(), ref _errorMessage);
            }

            private bool _benchmarkPassed;

            public bool BenchmarkPassed {
                get => _benchmarkPassed;
                set => Apply(value, ref _benchmarkPassed);
            }

            private string _benchmarkResults;

            [CanBeNull]
            public string BenchmarkResults {
                get => _benchmarkResults;
                set => Apply(value, ref _benchmarkResults);
            }

            private string _errorDescription = AppStrings.Common_MoreInformationInMainLog;

            public string ErrorDescription {
                get => _errorDescription;
                set => Apply(value, ref _errorDescription);
            }

            private BaseFinishedData _finishedData;

            public BaseFinishedData FinishedData {
                get => _finishedData;
                set {
                    if (Equals(value, _finishedData)) return;
                    _finishedData = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        private void OnScrollMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = sender as ScrollViewer ?? (sender as FrameworkElement)?.FindVisualChild<ScrollViewer>();
            scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void OnClosed(object sender, EventArgs e) { }
        private void OnLapsTableLoaded(object sender, RoutedEventArgs e) { }

        private void OnPlayersTableLoaded(object sender, RoutedEventArgs e) {
            var columns = ((DataGrid)sender).Columns.TakeLast(3).ToList();
            this.AddWidthCondition(1000).Add(columns[1]).Add(columns[2])
                .Add(x => {
                    columns[0].Width = x ? 100 : 140;
                    columns[0].HeaderStyle = (Style)FindResource(x ?
                            "DataGridColumnHeader.RightAlignment" : "DataGridColumnHeader.RightAlignment.FarRight");
                    ((DataGridTemplateColumn)columns[0]).CellTemplate = (DataTemplate)FindResource(x ?
                            "TotalTimeDeltaTemplate" : "TotalTimeDeltaTemplate.FarRight");
                    if (x) {
                        FancyHints.GameDialogTableSize.MaskAsUnnecessary();
                    }
                });

            if (ActualWidth < 1000d) {
                FancyHints.GameDialogTableSize.Trigger();
            } else {
                FancyHints.GameDialogTableSize.MaskAsUnnecessary();
            }
        }

        private void OnDataGridMouseDown(object sender, MouseButtonEventArgs e) {
            var grid = (DataGrid)sender;
            if (grid.IsMouseOverItem(grid.SelectedItem)) {
                grid.SelectedItem = null;
                e.Handled = true;
            }
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e) {
            if (e.PropertyName == "Tyres") {
                e.Column.HeaderStyle = (Style)FindResource("DataGridColumnHeader.Small");
            } else {
                e.Column.Width = e.PropertyName == "Lap" ? 60 : 100;
                e.Column.HeaderStyle = (Style)FindResource("DataGridColumnHeader.RightAlignment.Small");
                e.Column.CellStyle = (Style)FindResource("DataGridCell.Transparent.RightAlignment");
            }
        }
    }
}