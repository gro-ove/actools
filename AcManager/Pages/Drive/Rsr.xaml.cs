using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Properties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Drive {
    public partial class Rsr {
        public static AssistsViewModel Assists { get; } = new AssistsViewModel("rsrassists");

        private RsrViewModel Model => (RsrViewModel)DataContext;

        public Rsr() {
            DataContext = new RsrViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
            InitializeComponent();
            WebBrowser.SetScriptProvider(new ScriptProvider(Model));
        }

        public static Task RunAsync(string eventId) {
            return new RsrViewModel() {
                EventId = eventId
            }.Go();
        }

        public class RsrViewModel : NotifyPropertyChanged {
            private const string KeyGhostCar = "Rsr.GhostCar";

            internal RsrViewModel() {
                GhostCar = ValuesStorage.GetBool(KeyGhostCar, true);
            }

            public string StartPage => "http://www.radiators-champ.com/RSRLiveTiming/index.php?page=hottest_combos";

            private string _eventId;

            public string EventId {
                get { return _eventId; }
                set {
                    if (Equals(value, _eventId)) return;
                    _eventId = value;
                    OnPropertyChanged();
                    GoCommand.OnCanExecuteChanged();

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
                get { return _ghostCar; }
                set {
                    if (Equals(value, _ghostCar)) return;
                    _ghostCar = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeyGhostCar, value);
                }
            }

            private CarObject _car;

            public CarObject Car {
                get { return _car; }
                set {
                    if (Equals(value, _car)) return;
                    _car = value;
                    OnPropertyChanged();
                }
            }

            private CarSkinObject _carSkin;

            public CarSkinObject CarSkin {
                get { return _carSkin; }
                set {
                    if (Equals(value, _carSkin)) return;
                    _carSkin = value;
                    OnPropertyChanged();
                }
            }

            private TrackBaseObject _track;

            public TrackBaseObject Track {
                get { return _track; }
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();
                }
            }

            private async Task<Tuple<string, string>>  LoadData() {
                Car = null;
                CarSkin = null;
                Track = null;

                string uri;
                if (EventId.Contains("/")) {
                    var splitted = EventId.Split('/');
                    uri = $"http://www.radiators-champ.com/RSRLiveTiming/index.php?page=rank&track={splitted[0]}&car={splitted[1]}";
                } else {
                    uri = $"http://www.radiators-champ.com/RSRLiveTiming/index.php?page=event_rank&eventId={EventId}";
                }

                string page;
                using (var client = new WebClient {
                    Headers = {
                        [HttpRequestHeader.UserAgent] = "Assetto Corsa Launcher",
                        ["X-User-Agent"] = CmApiProvider.UserAgent
                    }
                }) {
                    page = await client.DownloadStringTaskAsync(uri);
                }

#if DEBUG
                File.WriteAllText(FilesStorage.Instance.GetFilename("Logs", "RSR.txt"), page);
#endif

                var carIdMatch = Regex.Match(page, @"\bdata-car=""([\w-]+)""");
                var trackIdMatch = Regex.Match(page, @"\bdata-track=""([\w-]+)""");
                if (!carIdMatch.Success || !trackIdMatch.Success) return null;

                var carId = carIdMatch.Groups[1].Value;
                var trackId = trackIdMatch.Groups[1].Value;

                Car = CarsManager.Instance.GetById(carId);
                CarSkin = Car?.SelectedSkin;
                Track = TracksManager.Instance.GetById(trackId);

                return new Tuple<string, string>(carId, trackId);
            }

            public async Task Go() {
                try {
                    var app = PythonAppsManager.Instance.GetById("RsrLiveTime");
                    if (app == null) {
                        throw new InformativeException("RSR app is missing", @"Download it [url=""http://www.radiators-champ.com/RSRLiveTiming/""]here[/url].");
                    }

                    if (!app.Enabled) {
                        app.ToggleCommand.Execute(null);
                        await Task.Delay(500);
                    }

                    if (!app.Enabled) {
                        throw new InformativeException("RSR app is disabled", @"You need to enable it first.");
                    }

                    var ids = await LoadData();
                    if (ids == null) {
                        throw new InformativeException("Invalid RSR parameters", "Make sure RSR website is working properly.");
                    }

                    if (Car == null) {
                        throw new InformativeException($"Car “{ids.Item1}” is missing", @"[url=""https://docs.google.com/document/d/18fOaz0jRqtOssWaSw7CvRF9x7acizD-jdChEHd6ot-0/edit""]Here[/url] is the list of mods used by RSR (download links included).");
                    }

                    if (Track == null) {
                        throw new InformativeException($"Car “{ids.Item2}” is missing", @"[url=""https://docs.google.com/document/d/18fOaz0jRqtOssWaSw7CvRF9x7acizD-jdChEHd6ot-0/edit""]Here[/url] is the list of mods used by RSR (download links included).");
                    }

                    await GameWrapper.StartAsync(new Game.StartProperties {
                        BasicProperties = new Game.BasicProperties {
                            CarId = Car.Id,
                            TrackId = Track.Id,
                            CarSkinId = CarSkin?.Id
                        },
                        AssistsProperties = new Game.AssistsProperties {
                            Abs = AssistState.Off,
                            AutoBlip = false,
                            AutoBrake = false,
                            AutoClutch = false,
                            AutoShifter = false,
                            Damage = 100,
                            FuelConsumption = true,
                            TyreWearMultipler = 1d,
                            IdealLine = false,
                            SlipSteamMultipler = 1d,
                            StabilityControl = 0d,
                            TractionControl = AssistState.Factory,
                            TyreBlankets = true,
                            VisualDamage = true
                        },
                        ConditionProperties = new Game.ConditionProperties {
                            AmbientTemperature = 26d,
                            RoadTemperature = 32d,
                            CloudSpeed = 1d,
                            SunAngle = 0,
                            TimeMultipler = 1d,
                            WeatherName = WeatherManager.Instance.GetDefault()?.Id
                        },
                        TrackProperties = Game.GetDefaultTrackPropertiesPreset().Properties,
                        ModeProperties = new Game.HotlapProperties {
                            SessionName = "RSR Hotlap",
                            Penalties = true,
                            GhostCar = GhostCar,
                            GhostCarAdvantage = 0d
                        }
                    });
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t start race", e);
                }
            }

            private AsyncCommand _goCommand;

            public AsyncCommand GoCommand => _goCommand ?? (_goCommand = new AsyncCommand(o => Go(), o => EventId != null));
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        [ComVisible(true)]
        public class ScriptProvider : BaseScriptProvider {
            private readonly RsrViewModel _model;

            public ScriptProvider(RsrViewModel model) {
                _model = model;
            }

            public void SetEventId(string value) {
                _model.EventId = value;
            }
        }

        private void WebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            var uri = e.Uri.ToString();
            var match = Regex.Match(uri, @"\beventId=(\d+)");
            if (match.Success) {
                Model.EventId = match.Groups[1].Value;
            } else {
                var trackId = Regex.Match(uri, @"\btrack(?:Id)?=(\d+)");
                var carId = Regex.Match(uri, @"\bcar(?:Id)?=(\d+)");
                if (trackId.Success && carId.Success) {
                    Model.EventId = trackId.Groups[1].Value + "/" + carId.Groups[1].Value;
                } else {
                    Model.EventId = null;
                }
            }

            if (e.Uri.ToString().StartsWith("http://www.radiators-champ.com/RSRLiveTiming/")) {
                WebBrowser.UserStyle = BinaryResources.RsrStyle;
            } else {
                WebBrowser.UserStyle = null;
            }
        }

        private void AssistsMore_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            new AssistsDialog(Assists).ShowDialog();
        }

        private void UIElement_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
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
