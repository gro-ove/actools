using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.InnerHelpers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using StringBasedFilter;

namespace AcManager.Pages.Miscellaneous {
    public partial class RaceResults : IParametrizedUriContent, ILoadableContent {
        private IFilter<RaceResultsObject> _filter;
        private List<RaceResultsObject> _entries;

        private ViewModel Model => (ViewModel)DataContext;

        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            _filter = filter == null ? null : Filter.Create(RaceResultsTester.Instance, filter);
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _entries = await Task.Run(() => GetEntries().If(_filter != null, v => v.Where(_filter.Test)).ToList());
        }

        public void Load() {
            _entries = GetEntries().If(_filter != null, v => v.Where(_filter.Test)).ToList();
        }

        public void Initialize() {
            DataContext = new ViewModel(_entries);
            InitializeComponent();
            this.OnActualUnload(Model);
        }

        private static IEnumerable<RaceResultsObject> GetEntries() {
            var d = new DirectoryInfo(RaceResultsStorage.Instance.SessionsDirectory);
            if (!d.Exists) return new RaceResultsObject[0];
            return d.GetFiles("*.json", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(x => x.LastWriteTime)
                    .Select(x => new RaceResultsObject(x));
        }

        private static IEnumerable<RaceResultsObject> GetUpdateEntries(IList<RaceResultsObject> existing) {
            var d = new DirectoryInfo(RaceResultsStorage.Instance.SessionsDirectory);
            if (!d.Exists) return new RaceResultsObject[0];
            return d.GetFiles("*.json", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(x => x.LastWriteTime)
                    .Select(x => {
                        var e = existing.FirstOrDefault(y => string.Equals(y.DisplayName, x.Name, StringComparison.Ordinal));
                        if (e != null && e.Date == x.LastWriteTime) return e;
                        return new RaceResultsObject(x);
                    });
        }

        public class WrappedCarObject : NotifyPropertyChanged {
            public WrappedCarObject(CarObject car, bool isPlayerCar) {
                Car = car;
                IsPlayerCar = isPlayerCar;
            }

            public CarObject Car { get; }
            public bool IsPlayerCar { get; }
        }

        public class RaceResultsTester : IParentTester<RaceResultsObject> {
            public static readonly RaceResultsTester Instance = new RaceResultsTester();

            public string ParameterFromKey(string key) {
                return null;
            }

            public bool Test(RaceResultsObject obj, string key, ITestEntry value) {
                switch (key) {
                    case "date":
                        return value.Test(obj.Date);
                    case "age":
                        return value.Test((DateTime.Now - obj.Date).TotalDays);
                    case "mode":
                        return value.Test(obj.ModeSummary);
                    case "track":
                        return value.Test(obj.Track?.Name ?? obj.Parsed?.TrackId);
                    case "playercar":
                        return value.Test(obj.PlayerCar?.ForceGetValue().Result?.Name);
                    case "sessions":
                        return value.Test(obj.SessionNames);
                    case "details":
                        return value.Test(obj.ModeDetails?.ForceGetValue().Result);
                    case "summary":
                        return value.Test(obj.Summary);
                    case "online":
                        return value.Test(obj.JoinOnlineServerCommand.IsAbleToExecute);
                    case "quickdrive":
                    case "qd":
                    case "quick":
                        return value.Test(obj.QuickDrivePreset != null);
                    case "car":
                        return obj.Cars.ForceGetValue().Result?.Any(x => value.Test(x.Car.DisplayName)) == true;
                    case null:
                        return obj.Cars.ForceGetValue().Result?.Any(x => value.Test(x.Car.DisplayName)) == true ||
                                value.Test(obj.Track?.Name ?? obj.Parsed?.TrackId);
                }

                return false;
            }

            public bool TestChild(RaceResultsObject obj, string key, IFilter filter) {
                switch (key) {
                    case null:
                    case "car":
                        return obj.Cars.ForceGetValue().Result?.Any(x => x.Car != null && filter.Test(CarObjectTester.Instance, x.Car)) == true;
                    case "playercar":
                        return obj.PlayerCar.ForceGetValue().Result.With(c => c != null && filter.Test(CarObjectTester.Instance, c));
                    case "track":
                        return obj.Track != null && filter.Test(TrackObjectBaseTester.Instance, obj.Track);
                }

                return false;
            }
        }

        public sealed class RaceResultsObject : Displayable {
            public string Filename { get; }
            public DateTime CreationTime { get; }
            public DateTime LastWriteTime { get; }
            public long Size { get; }

            public RaceResultsObject(FileInfo fileInfo) {
                Filename = fileInfo.FullName;
                CreationTime = fileInfo.CreationTime;
                LastWriteTime = fileInfo.LastWriteTime;
                Size = fileInfo.Length;
                DisplayName = fileInfo.Name;

                PlayerCar = Lazier.CreateAsync(() => CarsManager.Instance.GetByIdAsync(Parsed?.Players?.FirstOrDefault()?.CarId ?? ""));
                Cars = Lazier.CreateAsync(async () => (await (Parsed?.Players?.Select(x => x.CarId)
                                                                     .Distinct()
                                                                     .Select(x => CarsManager.Instance.GetByIdAsync(x ?? ""))
                                                                     .WhenAll() ??
                        Task.FromResult<IEnumerable<CarObject>>(null)).ConfigureAwait(false)).Select((x, y) => new WrappedCarObject(x, y == 0))
                                                                                             .OrderBy(x => x.Car.DisplayName).ToList());
                ModeDetails = Lazier.CreateAsync(GetModeDetails);
            }

            public Lazier<CarObject> PlayerCar { get; }
            public Lazier<List<WrappedCarObject>> Cars { get; }

#pragma warning disable 649
            public DateTime Date => _date.Get(() => LastWriteTime);
            private LazierThis<DateTime> _date;

            public string DateDeltaString => _dateDeltaString.Get(() => $"{(DateTime.Now - Date).ToReadableTime()} ago");
            private LazierThis<string> _dateDeltaString;

            public string DateString => _dateString.Get(() => $"{Date} ({DateDeltaString})");
            private LazierThis<string> _dateString;

            [CanBeNull]
            public JObject JObject => _jObject.Get(() => JObject.Parse(File.ReadAllText(Filename)));
            private LazierThis<JObject> _jObject;

            [CanBeNull]
            public Game.Result Parsed => _parsed.Get(() => JObject?.ToObject<Game.Result>());
            private LazierThis<Game.Result> _parsed;

            [CanBeNull]
            public TrackObjectBase Track => _track.Get(() => TracksManager.Instance.GetLayoutByKunosId(Parsed?.TrackId ?? ""));
            private LazierThis<TrackObjectBase> _track;

            [CanBeNull]
            public string ModeSummary => _modeSummary.Get(() => {
                var online = RaceIni["REMOTE"];
                if (online.GetBool("ACTIVE", false)) {
                    return AppStrings.Main_Online;
                }

                var special = RaceIni["SPECIAL_EVENT"].GetIntNullable("GUID");
                if (special.HasValue) {
                    return "Challenge";
                }

                if (QuickDrivePreset != null) {
                    return "Quick Drive";
                }

                return null;
            });
            private LazierThis<string> _modeSummary;

            [CanBeNull]
            public Lazier<string> ModeDetails { get; }
            private async Task<string> GetModeDetails() {
                var online = RaceIni["REMOTE"];
                if (online.GetBool("ACTIVE", false)) {
                    return online.GetNonEmpty("SERVER_NAME");
                }

                var special = RaceIni["SPECIAL_EVENT"].GetNonEmpty("GUID");
                if (special != null) {
                    await SpecialEventsManager.Instance.EnsureLoadedAsync();
                    return SpecialEventsManager.Instance.GetByGuid(special)?.DisplayName;
                }

                return null;
            }

            public string Summary => _summary.Get(() => {
                var parsed = Parsed;
                var lastSession = parsed?.Sessions?.LastOrDefault();
                if (lastSession == null) return null;

                var players = parsed.Players?.Length ?? 1;
                var playersLine = PluralizingConverter.PluralizeExt(players, "{0} player");
                var bestLapLine = lastSession.BestLaps?.FirstOrDefault(x => x.CarNumber == 0)?.Time.ToMillisecondsString() ?? "N/A";

                if (lastSession.Type == Game.SessionType.Race) {
                    return playersLine + ", taken place: " + ((lastSession.GetTakenPlacesPerCar()?.FirstOrDefault() + 1)?.ToInvariantString() ?? "N/A");
                }

                if (lastSession.Name == Game.TrackDaySessionName) {
                    return playersLine + ", best lap time: " + bestLapLine;
                }

                if (lastSession.Type == Game.SessionType.TimeAttack) {
                    return "Points collected: " + (parsed.GetExtraByType<Game.ResultExtraTimeAttack>()?.Points.ToInvariantString() ?? "N/A");
                }

                if (lastSession.Type == Game.SessionType.Drift) {
                    return "Points collected: " + (parsed.GetExtraByType<Game.ResultExtraDrift>()?.Points.ToInvariantString() ?? "N/A");
                }

                if (lastSession.Type == Game.SessionType.Drag) {
                    var ex = parsed.GetExtraByType<Game.ResultExtraDrag>();
                    return ex == null ? "N/A" :
                            $"{PluralizingConverter.PluralizeExt(ex.Wins, "{0} win")} out of {PluralizingConverter.PluralizeExt(ex.Runs, "{0} run")}";
                }

                if (lastSession.Type == Game.SessionType.Hotlap || lastSession.Type == Game.SessionType.Practice
                        || lastSession.Name == Game.TrackDaySessionName) {
                    return "Best lap time: " + bestLapLine;
                }

                return "?";
            });
            private LazierThis<string> _summary;

            [NotNull]
            public IniFile RaceIni => _raceIni.Get(() => IniFile.Parse(JObject?.GetStringValueOnly(RaceResultsStorage.KeyRaceIni) ?? ""));
            private LazierThis<IniFile> _raceIni;

            [CanBeNull]
            public string QuickDrivePreset => _quickDrivePreset.Get(() => JObject?.GetStringValueOnly(RaceResultsStorage.KeyQuickDrive));
            private LazierThis<string> _quickDrivePreset;

            [CanBeNull]
            public string SessionNames => _sessionNames.Get(() => {
                var sessions = Parsed?.Sessions;
                if (sessions == null) return null;

                // var online = RaceIni;
                return sessions.IsWeekendSessions() ? ToolsStrings.Common_Weekend :
                        Parsed?.Sessions?.Select(GameResultExtension.GetSessionName).JoinToReadableString();
            });
            private LazierThis<string> _sessionNames;
#pragma warning restore 649

            private DelegateCommand _showDetailsCommand;

            public DelegateCommand ShowDetailsCommand => _showDetailsCommand ?? (_showDetailsCommand = new DelegateCommand(() => {
                new GameDialog(Parsed).ShowDialog();
            }));

            private string SetQuickDriveRaceGrid() {
                var parsed = Parsed;
                var players = parsed?.Players;
                var sessions = parsed?.Sessions;
                if (players == null || sessions == null || players.Length == 0) return null;

                var opponents = players.Skip(1).Select(x => new {
                    x.Name,
                    Car = CarsManager.Instance.GetById(x.CarId ?? ""),
                    SkinId = x.CarSkinId
                }).Where(x => x.Car != null).ToList();
                if (opponents.Count <= 0) return null;

                var vm = new RaceGridViewModel(keySaveable: null) {
                    LoadingFromOutside = true,
                    Mode = BuiltInGridMode.Custom,
                    FilterValue = null,
                    RandomSkinsFilter = null
                };

                vm.NonfilteredList.ReplaceEverythingBy(opponents.Select(x => {
                    var match = Regex.Match(x.Name, @"^(.+) \((\d+(?:[,\.]\d+)?)%(?:, (\d+(?:[,\.]\d+)?)?%\))$");
                    if (match.Success) {
                        var cleanName = match.Groups[1].Value;
                        var aiLevel = match.Groups[2].Value.AsDouble();
                        var aiAggression = match.Groups[3].Success ? match.Groups[3].Value.AsDouble() : (double?)null;
                        return new RaceGridEntry(x.Car) {
                            CarSkin = x.Car.GetSkinById(x.SkinId),
                            Name = cleanName,
                            AiLevel = aiLevel,
                            AiAggression = aiAggression,
                        };
                    }

                    return new RaceGridEntry(x.Car) {
                        CarSkin = x.Car.GetSkinById(x.SkinId),
                        Name = x.Name
                    };
                }));

                return vm.ExportToPresetData();
            }

            private Uri GetQuickDriveMode(bool anyOpponents) {
                var sessions = Parsed?.Sessions;
                if (sessions == null) return QuickDrive.ModePractice;

                Uri mode;
                if (sessions.IsWeekendSessions()) {
                    mode = QuickDrive.ModeWeekend;
                } else if (sessions.Length == 1) {
                    if (sessions[0].Name == Game.TrackDaySessionName) {
                        mode = QuickDrive.ModeTrackday;
                    } else {
                        switch (sessions[0].Type) {
                            case Game.SessionType.Booking:
                            case Game.SessionType.Qualification:
                                mode = null;
                                break;
                            case Game.SessionType.Practice:
                                mode = QuickDrive.ModePractice;
                                break;
                            case Game.SessionType.Race:
                                mode = QuickDrive.ModeRace;
                                break;
                            case Game.SessionType.Hotlap:
                                mode = QuickDrive.ModeHotlap;
                                break;
                            case Game.SessionType.TimeAttack:
                                mode = QuickDrive.ModeTimeAttack;
                                break;
                            case Game.SessionType.Drift:
                                mode = QuickDrive.ModeDrift;
                                break;
                            case Game.SessionType.Drag:
                                mode = QuickDrive.ModeDrag;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                } else {
                    mode = null;
                }

                return mode == null ?
                        anyOpponents ? QuickDrive.ModeRace : QuickDrive.ModePractice :
                        mode;
            }

            private DelegateCommand _joinOnlineServerCommand;

            public DelegateCommand JoinOnlineServerCommand => _joinOnlineServerCommand ?? (_joinOnlineServerCommand = new DelegateCommand(() => {
                var online = RaceIni["REMOTE"];
                if (online.GetBool("ACTIVE", false)) {
                    var ip = online.GetNonEmpty("SERVER_IP");
                    var port = online.GetNonEmpty("SERVER_HTTP_PORT");
                    var password = online.GetNonEmpty("PASSWORD");
                    var link = $@"acmanager://race/online/join?ip={ip}&httpPort={port}";
                    if (!string.IsNullOrWhiteSpace(password)) {
                        link += $@"&password={OnlineServer.EncryptSharedPassword($@"{ip}:{port}", password)}";
                    }

                    ArgumentsHandler.ProcessArguments(new[] { link }).Forget();
                }
            }, () => RaceIni["REMOTE"].GetBool("ACTIVE", false)));

            private AsyncCommand _startRaceCommand;

            public AsyncCommand StartRaceCommand => _startRaceCommand ?? (_startRaceCommand = new AsyncCommand(async () => {
                var special = RaceIni["SPECIAL_EVENT"].GetNonEmpty("GUID");
                if (special != null) {
                    await SpecialEventsManager.Instance.EnsureLoadedAsync();
                    SpecialEventsManager.Instance.GetByGuid(special)?.GoCommand.ExecuteAsync(null);
                    return;
                }

                if (QuickDrivePreset != null) {
                    await QuickDrive.RunAsync(serializedPreset: QuickDrivePreset);
                    return;
                }

                var players = Parsed?.Players;
                if (!(players?.Length > 0)) return;

                var raceGrid = SetQuickDriveRaceGrid();
                await QuickDrive.RunAsync(
                        CarsManager.Instance.GetById(players[0].CarId ?? ""), players[0].CarSkinId,
                        track: Track, mode: GetQuickDriveMode(raceGrid != null), serializedRaceGrid: raceGrid);
            }, () => Parsed != null));

            private AsyncCommand _setupRaceCommand;

            public AsyncCommand SetupRaceCommand => _setupRaceCommand ?? (_setupRaceCommand = new AsyncCommand(async () => {
                var special = RaceIni["SPECIAL_EVENT"].GetNonEmpty("GUID");
                if (special != null) {
                    await SpecialEventsManager.Instance.EnsureLoadedAsync();
                    var e = SpecialEventsManager.Instance.GetByGuid(special);
                    if (e != null) {
                        SpecialEvents.Show(e);
                    } else {
                        NonfatalError.Notify("Can’t show event with GUID=" + special, "Event not found.");
                    }
                    return;
                }

                if (QuickDrivePreset != null) {
                    QuickDrive.Show(serializedPreset: QuickDrivePreset);
                    return;
                }

                var players = Parsed?.Players;
                if (!(players?.Length > 0)) return;

                var raceGrid = SetQuickDriveRaceGrid();
                QuickDrive.Show(
                        CarsManager.Instance.GetById(players[0].CarId ?? ""), players[0].CarSkinId,
                        track: Track, mode: GetQuickDriveMode(raceGrid != null), serializedRaceGrid: raceGrid);
            }));

            private DelegateCommand _viewInExplorerCommand;

            public DelegateCommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(() => {
                WindowsHelper.ViewFile(Filename);
            }));

            private bool _isDeleted;

            public bool IsDeleted {
                get => _isDeleted;
                private set {
                    if (Equals(value, _isDeleted)) return;
                    _isDeleted = value;
                    OnPropertyChanged();
                }
            }

            private bool _isDeleting;

            public bool IsDeleting {
                get => _isDeleting;
                private set {
                    if (value == _isDeleting) return;
                    _isDeleting = value;
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _deleteCommand;

            public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
                var isDeleting = IsDeleting;
                try {
                    IsDeleting = true;
                    FileUtils.Recycle(Filename);
                    if (!File.Exists(Filename)) {
                        IsDeleted = true;
                    }
                } finally {
                    IsDeleting = isDeleting;
                }
            }));
        }

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            public ChangeableObservableCollection<RaceResultsObject> Entries { get; }

            private readonly DirectoryWatcher _watcher;
            private readonly Busy _busy = new Busy(true);

            public ViewModel(List<RaceResultsObject> entries) {
                Entries = new ChangeableObservableCollection<RaceResultsObject>(entries);
                Entries.ItemPropertyChanged += OnPropertyChanged;
                _watcher = new DirectoryWatcher(RaceResultsStorage.Instance.SessionsDirectory);
                _watcher.Update += OnUpdate;
            }

            private void OnUpdate(object sender, FileSystemEventArgs fileSystemEventArgs) {
                _busy.DoDelay(() => Entries.ReplaceEverythingBy(GetUpdateEntries(Entries)), 300);
            }

            private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(RaceResultsObject.IsDeleting)) {
                    _busy.Delay(1000);
                }

                if (e.PropertyName == nameof(RaceResultsObject.IsDeleted)) {
                    Entries.Remove((RaceResultsObject)sender);
                }
            }

            public void Dispose() {
                _watcher.Dispose();
            }
        }

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 2) return;
            e.Handled = true;
            (((FrameworkElement)sender).DataContext as RaceResultsObject)?.ShowDetailsCommand.Execute();
        }

        private void OnTryAgainClick(object sender, RoutedEventArgs e) {
            var item = ((FrameworkElement)sender).DataContext as RaceResultsObject;
            if (item == null) return;

            e.Handled = true;
            new ContextMenu()
                    .If(item.JoinOnlineServerCommand.CanExecute, c => {
                        c.AddItem("Join Online", item.JoinOnlineServerCommand).AddSeparator();
                    })
                    .AddItem("Setup Race", item.SetupRaceCommand)
                    .AddItem("Start Race", item.StartRaceCommand, style: TryFindResource("GoMenuItem") as Style)
                    .IsOpen = true;
        }
    }
}
