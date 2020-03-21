using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Controls.ViewModels;
using AcManager.Properties;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using Newtonsoft.Json;

namespace AcManager.Pages.Drive {
    public partial class Srs2 : ILoadableContent {
        public static PluginsRequirement Requirement { get; } = new PluginsRequirement(KnownPlugins.CefSharp);

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await CarsManager.Instance.EnsureLoadedAsync();
            await TracksManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            CarsManager.Instance.EnsureLoaded();
            TracksManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
            if (SettingsHolder.Live.SrsCollectCombinations) {
                this.AddWidthCondition(1200).Add(t => Browser.LeftSideContent as FrameworkElement);
            } else {
                Browser.LeftSideContent = null;
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class RaceCombination : NotifyPropertyChanged {
            private TrackObjectBase _track;

            public TrackObjectBase Track {
                get => _track;
                set => Apply(value, ref _track);
            }

            private CarObject _selectedCar;

            public CarObject SelectedCar {
                get => _selectedCar;
                set => Apply(value, ref _selectedCar, () => _localRace?.RaiseCanExecuteChanged());
            }

            public BetterObservableCollection<CarObject> Cars { get; } = new BetterObservableCollection<CarObject>();

            private AsyncCommand _localRace;

            public AsyncCommand LocalRaceCommand => _localRace ?? (_localRace = new AsyncCommand(async () => {
                if (SelectedCar == null || Track == null) return;

                var opponentsCount = Math.Min(MathUtils.Random(8, 12), Track.SpecsPitboxesValue);
                var names = ValuesStorage.GetStringList("Srs.DriverNames").Select(x => new NameNationality {
                    Name = x,
                    Nationality = DataProvider.Instance.NationalitiesAndNamesList.RandomElementOrDefault()?.Nationality ?? "Italy"
                }).ToList();
                if (opponentsCount > names.Count) {
                    names.AddRange(GoodShuffle.Get(DataProvider.Instance.NationalitiesAndNamesList).Take(opponentsCount - names.Count));
                }

                var namesShuffle = GoodShuffle.Get(names);
                await GameWrapper.StartAsync(new Game.StartProperties {
                    BasicProperties = new Game.BasicProperties {
                        CarId = SelectedCar.Id,
                        CarSkinId = SelectedCar.SelectedSkin?.Id,
                        TrackId = Track.Id,
                        TrackConfigurationId = Track.LayoutId
                    },
                    AssistsProperties = AssistsViewModel.Instance.ToGameProperties(),
                    ConditionProperties = new Game.ConditionProperties {
                        AmbientTemperature = 18,
                        CloudSpeed = 1d,
                        RoadTemperature = 24,
                        SunAngle = Game.ConditionProperties.GetSunAngle(13 * 60 * 60),
                        TimeMultipler = 1d,
                        WeatherName = WeatherManager.Instance.GetDefault()?.Id,
                        WindDirectionDeg = 0d,
                        WindSpeedMin = 0d,
                        WindSpeedMax = 0d
                    },
                    TrackProperties = Game.GetDefaultTrackPropertiesPreset().Properties,
                    ModeProperties = new Game.RaceProperties {
                        AiLevel = 100,
                        Penalties = true,
                        JumpStartPenalty = Game.JumpStartPenaltyType.Pits,
                        StartingPosition = opponentsCount - 1,
                        RaceLaps = (int)(15d / Track.GuessApproximateLapDuration(SelectedCar).TotalMinutes).Ceiling(),
                        BotCars = GoodShuffle.Get(Cars).IgnoreOnce(SelectedCar).Take(opponentsCount)
                                .Select((x, i) => {
                                    var name = namesShuffle.Next;
                                    return new Game.AiCar {
                                        AiAggression = 100,
                                        AiLevel = 100,
                                        CarId = x.Id,
                                        SkinId = x.SkinsManager.WrappersList.Where(y => y.Value.Enabled).RandomElementOrDefault()?.Id,
                                        DriverName = name?.Name,
                                        Nationality = name?.Nationality
                                    };
                                })
                    }
                });
            }, () => SelectedCar != null && Track != null));

            private DelegateCommand _quickDrive;

            public DelegateCommand QuickDriveCommand
                => _quickDrive ?? (_quickDrive = new DelegateCommand(() => { QuickDrive.Show(SelectedCar, track: Track, mode: QuickDrive.ModePractice); }));
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {
                if (RaceCombinations == null) {
                    RaceCombinations = new BetterObservableCollection<RaceCombination>();
                }
                CurrentRaceCombination = new RaceCombination();
            }

            public static BetterObservableCollection<RaceCombination> RaceCombinations { get; set; }

            public RaceCombination CurrentRaceCombination { get; set; }

            public bool MakingNewList { get; set; }

            public void CommitIfPossible() {
                if (CurrentRaceCombination.Track != null && CurrentRaceCombination.Cars.Count > 0
                        && RaceCombinations.IndexOf(CurrentRaceCombination) == -1) {
                    if (MakingNewList) {
                        MakingNewList = false;
                        RaceCombinations.Clear();
                    }
                    CurrentRaceCombination.SelectedCar = CurrentRaceCombination.Cars.FirstOrDefault();
                    RaceCombinations.Add(CurrentRaceCombination);
                }
            }

            public void OnPageStart(object sender, EventArgs e) {
                MakingNewList = true;
                CurrentRaceCombination = new RaceCombination();
            }

            public void OnDriverNames(object sender, SrsFixAcCompatibleApiBridge.DriverNamesEventArgs e) {
                ValuesStorage.Storage.SetStringList("Srs.DriverNames",
                        e.Names.Concat(ValuesStorage.GetStringList("Srs.DriverNames")).Distinct().Take(40));
            }

            public void OnTrackAccessed(object sender, AcCompatibleApiBridge.AcItemAccessedEventArgs e) {
                var track = TracksManager.Instance.GetLayoutById(e.Id);
                if (CurrentRaceCombination.Track != track) {
                    CurrentRaceCombination = new RaceCombination {
                        Track = track
                    };
                    CommitIfPossible();
                }
            }

            public void OnCarAccessed(object o, AcCompatibleApiBridge.AcItemAccessedEventArgs args) {
                var car = CarsManager.Instance.GetById(args.Id);
                if (car != null) {
                    CurrentRaceCombination.Cars.Add(car);
                    CommitIfPossible();
                }
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            var web = (WebBlock)sender;
            var styleProvider = new StyleProvider();
            var isThemeBright = ((Color)Application.Current.Resources[@"WindowBackgroundColor"]).GetBrightness() > 0.4;
            web.SetJsBridge<SrsFixAcCompatibleApiBridge>(b => {
                b.StyleProvider = styleProvider;
                b.IsThemeBright = isThemeBright;
                b.PageStart += Model.OnPageStart;
                b.DriverNames += Model.OnDriverNames;
                b.TrackAccessed += Model.OnTrackAccessed;
                b.CarAccessed += Model.OnCarAccessed;
            });
            web.StyleProvider = styleProvider;
        }

        internal class StyleProvider : ICustomStyleProvider {
            public bool TransparentBackgroundSupported;

            private static string PrepareStyle(string style, bool transparentBackgroundSupported) {
                var color = AppAppearanceManager.Instance.AccentColor;

                style = style
                        .Replace(@"#E20035", color.ToHexString())
                        .Replace(@"#CA0030", ColorExtension.FromHsb(color.GetHue(), color.GetSaturation(), color.GetBrightness() * 0.92).ToHexString());
                style = Regex.Replace(style, @"(?<=^|@media).+", m => ("" + m)
                        .Replace(@"no-ads", SettingsHolder.Plugins.CefFilterAds ? @"all" : @"print")
                        .Replace(@"transparent-bg", transparentBackgroundSupported ? @"all" : @"print"));

                return style;
            }

            public string GetStyle(string url, bool transparentBackgroundSupported) {
                TransparentBackgroundSupported = transparentBackgroundSupported;
                return SettingsHolder.Live.SrsCustomStyle && url.StartsWith(@"https://www.simracingsystem.com") ?
                        PrepareStyle(BinaryResources.SrsStyle, transparentBackgroundSupported) : null;
            }
        }

        private static string _srsFix;

        private static string GetSrsFix() {
            if (_srsFix == null) {
                var srsScript = Path.Combine(AcRootDirectory.Instance.RequireValue, @"launcher\themes\default\modules\srs\srs.js");
                try {
                    if (File.Exists(srsScript)) {
                        _srsFix = $@"<script>{File.ReadAllText(srsScript)}</script>";
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
                _srsFix = _srsFix ??
                        @"<script>function SRS_go(e, t, s, n, a, c, r, i, o) { $.get(""ac://setsetting/race?REMOTE/SERVER_IP="" + e), $.get(""ac://setsetting/race?REMOTE/SERVER_PORT="" + t), $.get(""ac://setsetting/race?REMOTE/SERVER_HTTP_PORT="" + s), $.get(""ac://setsetting/race?REMOTE/REQUESTED_CAR="" + n), $.get(""ac://setsetting/race?REMOTE/NAME="" + a), $.get(""ac://setsetting/race?REMOTE/TEAM=""), $.get(""ac://setsetting/race?REMOTE/PASSWORD=""), $.get(""ac://setsetting/race?CAR_0/SETUP=""), $.get(""ac://setsetting/race?CAR_0/MODEL=-""), $.get(""ac://setsetting/race?CAR_0/SKIN=""), $.get(""ac://setsetting/race?CAR_0/NATIONALITY="" + i), $.get(""ac://setsetting/race?CAR_0/NATION_CODE="" + o), $.get(""ac://setsetting/race?CAR_0/DRIVER_NAME="" + a), $.get(""ac://setsetting/race?REMOTE/GUID="" + r), $.get(""ac://setsetting/race?REPLAY/ACTIVE=0""), $.get(""ac://setsetting/race?REMOTE/ACTIVE=1""), $.get(""ac://start/""); }</script>";
            }
            return _srsFix;
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class SrsFixAcCompatibleApiBridge : AcCompatibleApiBridge {
            public SrsFixAcCompatibleApiBridge() {
                _srsFix = null;
                AcApiHosts.Add(@"simracingsystem.com");
            }

            internal StyleProvider StyleProvider { get; set; }

            internal bool IsThemeBright { get; set; }

            public EventHandler PageStart { get; set; }

            public class DriverNamesEventArgs : EventArgs {
                public List<string> Names { get; set; }
            }

            public EventHandler<DriverNamesEventArgs> DriverNames { get; set; }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                ActionExtension.InvokeInMainThreadAsync(() => PageStart?.Invoke(this, EventArgs.Empty));
                if (IsThemeBright
                        // GetStyle() is called before PageInject(), so it’s a good way to know if browser supports
                        // transparent background or not
                        || StyleProvider?.TransparentBackgroundSupported == false) {
                    replacements.Add(new KeyValuePair<string, string>(@"<body style=""background:none;"">", @"<body>"));
                }

                base.PageInject(url, toInject, replacements);
                toInject.Add(@"<script>!function(){ window.addEventListener('load', e => {
window.external.SetDriverNames(JSON.stringify([].map.call(document.querySelectorAll('#shoutbox [data-username]'), i => i.getAttribute('data-username'))));
}); }()</script>");
                toInject.Add(GetSrsFix());
            }

            public void SetDriverNames(string value) {
                try {
                    DriverNames?.Invoke(this, new DriverNamesEventArgs {
                        Names = JsonConvert.DeserializeObject<List<string>>(value).Distinct().ToList()
                    });
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }
        }
    }
}