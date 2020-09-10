using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls.Web;
using AcManager.Controls.ViewModels;
using AcManager.DiscordRpc;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public partial class Rsr {
        public static AssistsViewModel Assists { get; } = new AssistsViewModel("rsrassistsn");

        private ViewModel Model => (ViewModel)DataContext;

        public Rsr() {
            DataContext = new ViewModel();
            this.OnActualUnload(Model);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
            InitializeComponent();
            WebBrowser.SetJsBridge<JsBridge>(x => x.Model = Model);
            WebBrowser.StyleProvider = new StyleProvider();
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class JsBridge : JsBridgeCSharp {
            [CanBeNull]
            internal ViewModel Model;

            [UsedImplicitly]
            public void SetEventId(string value) {
                Sync(() => {
                    if (Model != null) {
                        Model.EventId = value;
                    }
                });
            }
        }

        public static Task<bool> RunAsync(string eventId) {
            return new ViewModel {
                EventId = eventId
            }.Go();
        }

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(30, "Preparing to race", "RSR");

            private const string KeyGhostCar = "Rsr.GhostCar";
            private const string KeyShowExtensionMessage = "Rsr.ExtMsg";

            internal ViewModel() {
                GhostCar = ValuesStorage.Get(KeyGhostCar, true);
                ShowExtensionMessage = ValuesStorage.Get(KeyShowExtensionMessage, true);
            }

            private bool _showExtensionMessage;

            public bool ShowExtensionMessage {
                get => _showExtensionMessage;
                set {
                    if (Equals(value, _showExtensionMessage)) return;
                    _showExtensionMessage = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeyShowExtensionMessage, value);
                }
            }

            private CommandBase _gotItCommand;

            public ICommand GotItCommand => _gotItCommand ?? (_gotItCommand = new DelegateCommand(() => {
                ShowExtensionMessage = false;
            }));

            public string StartPage => SteamIdHelper.Instance.IsReady ? @"http://www.radiators-champ.com/RSRLiveTiming/index.php?page=hottest_combos" : null;

            private string _eventId;

            public string EventId {
                get => _eventId;
                set {
                    if (Equals(value, _eventId)) return;
                    _eventId = value;
                    OnPropertyChanged();
                    GoCommand.RaiseCanExecuteChanged();

                    if (EventId != null) {
                        LoadData().Forget();
                    } else {
                        Car = null;
                        CarSkin = null;
                        Track = null;
                    }
                }
            }

            private bool _ghostCar;

            public bool GhostCar {
                get => _ghostCar;
                set {
                    if (Equals(value, _ghostCar)) return;
                    _ghostCar = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeyGhostCar, value);
                }
            }

            private CarObject _car;

            public CarObject Car {
                get => _car;
                set {
                    if (Equals(value, _car)) return;
                    _car = value;
                    OnPropertyChanged();
                    _discordPresence?.Car(value);
                }
            }

            private CarSkinObject _carSkin;

            public CarSkinObject CarSkin {
                get => _carSkin;
                set => Apply(value, ref _carSkin);
            }

            private TrackObjectBase _track;

            public TrackObjectBase Track {
                get => _track;
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();
                    _discordPresence?.Track(value);
                }
            }

            private async Task<Tuple<string, string>> LoadData() {
                Car = null;
                CarSkin = null;
                Track = null;

                string uri;
                if (EventId.Contains(@"/")) {
                    var splitted = EventId.Split('/');
                    uri = $"http://www.radiators-champ.com/RSRLiveTiming/index.php?page=rank&track={splitted[0]}&car={splitted[1]}";
                } else {
                    uri = $"http://www.radiators-champ.com/RSRLiveTiming/index.php?page=event_rank&eventId={EventId}";
                }

                string page;
                using (var client = new WebClient {
                    Headers = {
                        [HttpRequestHeader.UserAgent] = InternalUtils.GetKunosUserAgent(),
                        [@"X-User-Agent"] = CmApiProvider.UserAgent
                    }
                }) {
                    page = await client.DownloadStringTaskAsync(uri);
                }

                var carIdMatch = Regex.Match(page, @"\bdata-car=""([\w-]+)""");
                var trackIdMatch = Regex.Match(page, @"\bdata-track=""([\w-]+)""");
                var trackLayoutIdMatch = Regex.Match(page, @"\bdata-track-layout=""([\w-]+)""");
                if (!carIdMatch.Success || !trackIdMatch.Success) return null;

                var carId = carIdMatch.Groups[1].Value;
                var trackId = trackIdMatch.Groups[1].Value;
                var trackLayoutId = trackLayoutIdMatch.Success ? trackLayoutIdMatch.Groups[1].Value : null;

                if (trackLayoutId == trackId) {
                    trackLayoutId = null; // TODO: temporary fix
                }

                Car = CarsManager.Instance.GetById(carId);
                CarSkin = Car?.SelectedSkin;
                Track = TracksManager.Instance.GetLayoutById(trackId, trackLayoutId);

                return new Tuple<string, string>(carId, trackId);
            }

            public async Task<bool> Go() {
                if (EventId == null) return false;

                try {
                    var app = PythonAppsManager.Instance.GetById(RsrMark.AppId);
                    if (app == null) {
                        throw new InformativeException(AppStrings.Rsr_AppIsMissing, AppStrings.Rsr_AppIsMissing_Commentary);
                    }

                    if (!app.Enabled) {
                        app.ToggleCommand.Execute(null);
                        await Task.Delay(500);
                    }

                    if (!app.Enabled) {
                        throw new InformativeException(AppStrings.Rsr_AppIsDisabled, AppStrings.Rsr_AppIsDisabled_Commentary);
                    }

                    var ids = await LoadData();
                    if (ids == null) {
                        throw new InformativeException(AppStrings.Rsr_InvalidParameters, AppStrings.Rsr_InvalidParameters_Commentary);
                    }

                    if (Car == null) {
                        throw new InformativeException(string.Format(ToolsStrings.AcError_CarIsMissing, ids.Item1), AppStrings.Rsr_ContentIsMissing_Commentary);
                    }

                    if (Track == null) {
                        throw new InformativeException(string.Format(ToolsStrings.AcError_TrackIsMissing, ids.Item2), AppStrings.Rsr_ContentIsMissing_Commentary);
                    }

                    await GameWrapper.StartAsync(new Game.StartProperties {
                        BasicProperties = new Game.BasicProperties {
                            CarId = Car.Id,
                            TrackId = Track.Id,
                            TrackConfigurationId = Track.LayoutId,
                            CarSkinId = CarSkin?.Id,
                        },
                        AssistsProperties = Assists.ToGameProperties(),
                        ConditionProperties = new Game.ConditionProperties {
                            AmbientTemperature = 26d,
                            RoadTemperature = 32d,
                            CloudSpeed = 1d,
                            SunAngle = 0,
                            TimeMultipler = 1d,
                            WeatherName = WeatherManager.Instance.GetDefault()?.Id,
                            WindDirectionDeg = 0d,
                            WindSpeedMin = 0d,
                            WindSpeedMax = 0d
                        },
                        TrackProperties = Game.GetDefaultTrackPropertiesPreset().Properties,
                        ModeProperties = new Game.HotlapProperties {
                            SessionName = AppStrings.Rsr_SessionName,
                            Penalties = true,
                            GhostCar = GhostCar,
                            GhostCarAdvantage = 0d
                        },
                        AdditionalPropertieses = {
                            new RsrMark()
                        }
                    });

                    return true;
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Common_CannotStartRace, e);
                    return false;
                }
            }

            private AsyncCommand _goCommand;

            public AsyncCommand GoCommand => _goCommand ?? (_goCommand = new AsyncCommand(Go, () => EventId != null));

            public void Dispose() {
                _discordPresence?.Dispose();
            }
        }

        private class StyleProvider : ICustomStyleProvider {
            private string GetCustomStyle() {
                var color = AppAppearanceManager.Instance.AccentColor;
                return BinaryResources.RsrStyle
                                      .Replace(@"#E20035", color.ToHexString())
                                      .Replace(@"#CA0030", ColorExtension.FromHsb(color.GetHue(), color.GetSaturation(), color.GetBrightness() * 0.92).ToHexString());
            }

            public string GetStyle(string url, bool transparentBackgroundSupported) {
                return SettingsHolder.Live.RsrCustomStyle && url.StartsWith(@"http://www.radiators-champ.com/RSRLiveTiming/") ?
                        GetCustomStyle() : null;
            }
        }

        private void OnPageLoaded(object sender, WebTabEventArgs e) {
            var uri = e.Tab.LoadedUrl;
            if (uri == null) return;

            var match = Regex.Match(uri, @"\beventId=(\d+)");
            if (match.Success) {
                Model.EventId = match.Groups[1].Value;
            } else {
                var trackId = Regex.Match(uri, @"\btrack(?:Id)?=(\d+)");
                var carId = Regex.Match(uri, @"\bcar(?:Id)?=(\d+)");
                if (trackId.Success && carId.Success) {
                    Model.EventId = trackId.Groups[1].Value + @"/" + carId.Groups[1].Value;
                } else {
                    Model.EventId = null;
                }
            }

            if (uri.Contains(@"page=setups")) {
                e.Tab.Execute(@"
window.addEventListener('load', function(){
    var ths = document.getElementsByTagName('th');
    for (var i=0; i<ths.length; i++) if (ths[i].innerHTML == 'Download') ths[i].innerHTML = 'Install';
    var hs = document.getElementsByTagName('a');
    for (var i=0, m; i<hs.length; i++) if (m = hs[i].href.match(/=download_setup&id=(\d+)/)) hs[i].href = 'acmanager://rsr/setup?id=' + m[1];
}, false);");
            }
        }

        private void OnAssistsClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled) return;
            e.Handled = true;
            new AssistsDialog(Assists).ShowDialog();
        }

        private void OnSkinLiveryClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled) return;
            e.Handled = true;

            var control = new CarBlock {
                Car = Model.Car,
                SelectedSkin = Model.CarSkin,
                SelectSkin = SettingsHolder.Drive.KunosCareerUserSkin,
                OpenShowroom = true
            };

            var dialog = new ModernDialog {
                Content = control,
                Width = 640,
                Height = 720,
                MaxWidth = 640,
                MaxHeight = 720,
                SizeToContent = SizeToContent.Manual,
                Title = Model.Car.DisplayName
            };

            dialog.Buttons = new[] { dialog.OkButton, dialog.CancelButton };
            dialog.ShowDialog();

            if (dialog.IsResultOk) {
                Model.CarSkin = control.SelectedSkin;
            }
        }
    }
}
