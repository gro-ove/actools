using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Internal;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json.Linq;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

namespace AcManager.Pages.Drive {
    public partial class Srs {
        private ViewModel Model => (ViewModel)DataContext;

        public Srs() {
            DataContext = new ViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
            InitializeComponent();
            WebBrowser.SetScriptProvider(new ScriptProvider(Model));
        }

        public sealed class ServerInformation : Displayable {
            public string Ip { get; }

            public int? Port { get; }

            public int? PortHttp { get; }

            public string Password { get; }

            public ServerInformation(string ip, int? port, int? portHttp, string password, string name) {
                Ip = string.IsNullOrWhiteSpace(ip) ? null : ip;
                Port = port;
                PortHttp = portHttp;
                Password = string.IsNullOrWhiteSpace(password) ? null : password;
                DisplayName = string.IsNullOrWhiteSpace(name) ? null : name;
            }
        }

        public sealed class PlayerInformation : Displayable {
            public PlayerInformation(string name, string team, string nationality) {
                DisplayName = string.IsNullOrWhiteSpace(name) ? null : name;
                Team = string.IsNullOrWhiteSpace(team) ? null : team;
                Nationality = string.IsNullOrWhiteSpace(nationality) ? null : nationality;

                if (name != null) {
                    SrsMark.SetName(name);
                }
            }

            public string Team { get; }

            public string Nationality { get; }
        }

        public class ViewModel : NotifyPropertyChanged {
            private const string KeyShowExtensionMessage = "Srs.ExtMsg";

            internal ViewModel() {
                ShowExtensionMessage = ValuesStorage.GetBool(KeyShowExtensionMessage, true);
            }

            private bool _showExtensionMessage;

            public bool ShowExtensionMessage {
                get { return AppKeyHolder.IsAllRight && _showExtensionMessage; }
                set {
                    if (Equals(value, _showExtensionMessage)) return;
                    _showExtensionMessage = value;
                    OnPropertyChanged();
                    ValuesStorage.Set(KeyShowExtensionMessage, value);
                }
            }

            private RelayCommand _gotItCommand;

            public RelayCommand GotItCommand => _gotItCommand ?? (_gotItCommand = new RelayCommand(o => {
                ShowExtensionMessage = false;
            }));

            public string StartPage => SteamIdHelper.Instance.IsReady ? @"http://www.simracingsystem.com/race4.php" : null;

            public void Reset() {
                Server = null;
                Player = null;
                CarId = null;
                StartTime = null;
                QuitUrl = null;
            }

            private string _quitUrl;

            public string QuitUrl {
                get { return _quitUrl; }
                set {
                    if (Equals(value, _quitUrl)) return;
                    _quitUrl = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanQuit));
                    CommandManager.InvalidateRequerySuggested();
                }
            }

            public bool CanQuit => _quitUrl != null;

            private PlayerInformation _player;

            public PlayerInformation Player {
                get { return _player; }
                set {
                    if (Equals(value, _player)) return;
                    _player = value;
                    OnPropertyChanged();
                    _goCommand?.OnCanExecuteChanged();
                    Update();
                }
            }

            private ServerInformation _server;

            public ServerInformation Server {
                get { return _server; }
                set {
                    if (Equals(value, _server)) return;
                    _server = value;
                    OnPropertyChanged();
                    _goCommand?.OnCanExecuteChanged();
                    Update();
                }
            }

            private bool _available;

            private void Update() {
                var available = GoCommand.CanExecute(null);
                if (available == _available) return;

                _available = available;
                if (_available) {
                    Toast.Show(AppStrings.Srs_ReadyNotificationHeader, AppStrings.Srs_ReadyNotification, () => {
                        Go().Forget();
                    });
                }
            }

            private DateTime? _startTime;

            public DateTime? StartTime {
                get { return _startTime; }
                set {
                    if (Equals(value, _startTime)) return;
                    _startTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LeftTime));
                }
            }

            public TimeSpan? LeftTime => _startTime.HasValue ? _startTime - DateTime.Now : null;

            public void OnTick() {
                if (_startTime.HasValue) {
                    OnPropertyChanged(nameof(LeftTime));
                }
            }

            private string _carId;

            public string CarId {
                get { return _carId; }
                set {
                    if (Equals(value, _carId)) return;
                    _carId = value;
                    OnPropertyChanged();

                    Car = string.IsNullOrWhiteSpace(value) ? null : CarsManager.Instance.GetById(value);
                    OnPropertyChanged(nameof(Car));
                    _goCommand?.OnCanExecuteChanged();

                    CarSkin = null;
                    Update();
                }
            }

            public CarObject Car { get; private set; }

            private CarSkinObject _carSkin;

            public CarSkinObject CarSkin {
                get { return _carSkin; }
                set {
                    if (Equals(value, _carSkin)) return;
                    _carSkin = value;
                    OnPropertyChanged();
                }
            }

            public async Task<bool> Go() {
                if (Server?.Ip == null || Player == null || CarId == null) return false;

                try {
                    if (Car == null) {
                        throw new InformativeException(string.Format(ToolsStrings.AcError_CarIsMissing, CarId), AppStrings.Srs_CarIsMissing_Commentary);
                    }

                    var anyTrack = TracksManager.Instance.GetDefault();
                    await GameWrapper.StartAsync(new Game.StartProperties {
                        BasicProperties = new Game.BasicProperties {
                            CarId = CarId,
                            CarSkinId = CarSkin?.Id,
                            TrackId = anyTrack?.Id,
                            TrackConfigurationId = anyTrack?.LayoutId
                        },
                        ModeProperties = new Game.OnlineProperties {
                            ServerName = Server.DisplayName,
                            ServerIp = Server.Ip,
                            ServerPort = Server.Port ?? 9615,
                            ServerHttpPort = Server.PortHttp,
                            Password = Server.Password,
                            Penalties = true,
                        },
                        AdditionalPropertieses = {
                            new SrsMark {
                                Name = Player.DisplayName,
                                Team = Player.Team,
                                Nationality = Player.Nationality
                            }
                        }
                    });

                    return true;
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Common_CannotStartRace, e);
                    return false;
                }
            }

            private AsyncCommand _goCommand;

            public AsyncCommand GoCommand => _goCommand ?? (_goCommand =
                    new AsyncCommand(o => Go(), o => _server?.Ip != null && _server.Port.HasValue && _player != null && _carId != null));
        }

        private AsyncCommand _quitCommand;

        public AsyncCommand QuitCommand => _quitCommand ?? (_quitCommand = new AsyncCommand(async o => {
            WebBrowser.Navigate(Model.QuitUrl);
            await Task.Delay(500);
            WebBrowser.Navigate(Model.StartPage);
            await Task.Delay(500);
        }, o => Model.CanQuit));

        private RelayCommand _testCommand;

        public RelayCommand TestCommand => _testCommand ?? (_testCommand = new RelayCommand(o => {
            WebBrowser.Execute(@"location.reload(true)");
        }));

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class ScriptProvider : BaseScriptProvider {
            private readonly ViewModel _model;

            public ScriptProvider(ViewModel model) {
                _model = model;
            }

            public async void SetCars(string json) {
                if (json == null || Associated == null) return;

                var ids = JArray.Parse(json).ToObject<string[]>();
                var i = 0;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report(0);

                    foreach (var id in ids.NonNull()) {
                        var car = await CarsManager.Instance.GetByIdAsync(id);
                        if (car == null) continue;

                        await car.SkinsManager.EnsureLoadedAsync();

                        var skins = car.SkinsManager.LoadedOnly.ToList();
                        var skin = skins.FirstOrDefault();
                        if (skin == null) continue;

                        var liveries = new StringBuilder();
                        foreach (var x in skins) {
                            liveries.Append(
                                    $"<img data-skin-id='{HttpUtility.HtmlEncode(x.Id)}' width=24 style='margin-left:2px' src='data:image/png;base64,{Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(x.LiveryImage))}'>");
                        }

                        Associated.Execute($@"
document.querySelector('[id^=""{id}1""]').innerHTML = ""<img width=280 style='margin-right:10px' src='data:image/png;base64,{Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(skin.PreviewImage))}'>"";
document.querySelector('[id^=""{id}2""]').innerHTML = ""{HttpUtility.HtmlEncode(car.DisplayName)}<img width=48 style='margin-left:2px;margin-right:10px;margin-top:-10px;float:left' src='data:image/png;base64,{Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(car.LogoIcon))}'>"";
document.querySelector('[id^=""{id}3""]').innerHTML = ""{liveries}"";
document.querySelector('[id^=""{id}4""]').innerHTML = ""{HttpUtility.HtmlEncode(skin.Id)}"";

var l = document.querySelectorAll('[id^=""{id}3""] img');
for (var i = 0; i < l.length; i++){{
    l[i].addEventListener('click', function(){{
        var s = this.getAttribute('data-skin-id');
        document.querySelector('[id^=""{id}4""]').innerHTML = s;
        window.external.UpdatePreview(""{id}"", s);
    }}, false);                   
}}
");

                        waiting.Report(new AsyncProgressEntry(car.DisplayName, i++, ids.Length));
                    }
                }
            }

            public async void UpdatePreview(string carId, string skinId) {
                if (carId == null || skinId == null || Associated == null) return;

                var car = await CarsManager.Instance.GetByIdAsync(carId);
                if (car == null) return;

                var skin = await car.SkinsManager.GetByIdAsync(skinId);
                if (skin == null) return;

                Associated.Execute($@"document.querySelector('[id^=""{carId}1""] img').src = 'data:image/png;base64,{
                        Convert.ToBase64String(await FileUtils.ReadAllBytesAsync(skin.PreviewImage))}';");
            }

            public void SetParams(string json) {
                Logging.Write("SetParams(): " + json);

                if (json == null) {
                    _model.Reset();
                    return;
                }

                var obj = JObject.Parse(json);
                var carId = obj.GetStringValueOnly("REMOTE/REQUESTED_CAR");
                var carSkinId = obj.GetStringValueOnly("CAR_0/SKIN");
                _model.CarId = carId;
                _model.CarSkin = _model.Car == null ? null :
                        (string.IsNullOrWhiteSpace(carSkinId) ? null : _model.Car.GetSkinById(carSkinId)) ?? _model.Car.SelectedSkin;

                _model.Server = new ServerInformation(
                        obj.GetStringValueOnly("REMOTE/SERVER_IP"),
                        obj.GetIntValueOnly("REMOTE/SERVER_PORT"),
                        obj.GetIntValueOnly("REMOTE/SERVER_HTTP_PORT"),
                        obj.GetStringValueOnly("REMOTE/PASSWORD"),
                        obj.GetStringValueOnly("REMOTE/SERVER_NAME"));
                _model.Player = new PlayerInformation(
                        obj.GetStringValueOnly("REMOTE/NAME"),
                        obj.GetStringValueOnly("REMOTE/TEAM"),
                        obj.GetStringValueOnly("CAR_0/NATIONALITY"));

                var secondsLeft = obj.GetIntValueOnly("time");
                _model.StartTime = secondsLeft.HasValue ? DateTime.Now + TimeSpan.FromSeconds(secondsLeft.Value) : (DateTime?)null;

                _model.QuitUrl = obj.GetStringValueOnly("quit");
            }

            public void Go() {
                _model.Go().Forget();
            }
        }

        private string GetCustomStyle() {
            var color = AppAppearanceManager.Instance.AccentColor;
            return BinaryResources.SrsStyle
                                  .Replace(@"#E20035", color.ToHexString())
                                  .Replace(@"#CA0030", ColorExtension.FromHsb(color.GetHue(), color.GetSaturation(), color.GetBrightness() * 0.92).ToHexString());
        }

        private void WebBrowser_OnPageLoaded(object sender, PageLoadedEventArgs e) {
            var uri = e.Url;
            if (uri.StartsWith(@"http://www.simracingsystem.com/select.php?", StringComparison.OrdinalIgnoreCase)) {
                WebBrowser.Execute(@"
var s = document.createElement('style');
s.innerHTML = '#content { display: none !important }';
s.setAttribute('id', '__cm_style_tmp');
document.head.appendChild(s);");

                WebBrowser.Execute(@"
var g = document.getElementById('gui');
if (g){
    g.innerHTML = '" + (SteamIdHelper.Instance.Value ?? "") + @"';
    window.external.Log('GUID set: ' + g.innerHTML);

    var s = document.getElementById('__cm_style_tmp');
    if (s) s.parentNode.removeChild(s);
} else {
    window.external.Log('Nothing to set GUID to!');
}

var a = [];
var b = document.querySelectorAll('[onclick*=""./regsrs.php?""]');
for (var i = 0; i < b.length; i++){ var c = (b[i].getAttribute('onclick').match(/&h=(\w+)/)||{})[1]; if (c) a.push(c); }
window.external.SetCars(JSON.stringify(a));", true);
            } else if (uri.StartsWith(@"http://www.simracingsystem.com/race.php", StringComparison.OrdinalIgnoreCase)) {
                Logging.Write("WebBrowser_OnPageLoaded(): " + uri);
                WebBrowser.Execute(@"
window.external.Log('Here');
var b = document.querySelector('[onclick*=""REMOTE/REQUESTED_CAR""]');
if (!b){
    window.external.SetParams(null);
    return;
}

var o = {};
b.getAttribute('onclick').replace(/\/\/setsetting\/race\?(\w+\/\w+)=([^']*)/g, function(_, k, v){ o[k] = v == '' ? null : v; });

try {
    o['time'] = +b.parentNode.querySelector('script').innerHTML.match(/time:(\d+)/)[1];
} catch(e){}

try {
    if (b.value == 'Quit'){
        o['quit'] = location.host + '/' + b.getAttribute('onclick').match(/'(?:\.\/)?([^']*unregsrs[^']+)'/)[1];
    }
} catch(e){}

window.external.SetParams(JSON.stringify(o));

b.removeAttribute('onclick');
b.addEventListener('click', function (){
    window.external.Go();
}, false)", true);
            }

            WebBrowser.UserStyle = SettingsHolder.Live.SrsCustomStyle && uri.StartsWith(@"http://www.simracingsystem.com")
                    ? GetCustomStyle() : null;
        }

        private bool _loaded;
        private DispatcherTimer _timer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            _timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };
            _timer.Tick += OnTick;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            if (_timer != null) {
                _timer.Tick -= OnTick;
                _timer.Stop();
            }
        }
        private void OnTick(object sender, EventArgs e) {
            Model.OnTick();
        }

    }
}
