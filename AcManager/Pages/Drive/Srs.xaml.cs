using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.DiscordRpc;
using AcManager.Internal;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Drive {
    public partial class Srs {
        private ViewModel Model => (ViewModel)DataContext;

        public Srs() {
            DataContext = new ViewModel();
            this.OnActualUnload(Model);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control))
            });
            InitializeComponent();
            Model.SetTab(WebBrowser);
            WebBrowser.SetJsBridge<JsBridge>(x => x.Model = Model);
            WebBrowser.StyleProvider = new StyleProvider();
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class JsBridge : JsBridgeBase {
            [CanBeNull]
            internal ViewModel Model;

            [UsedImplicitly]
            public void SetCars(string json) {
                Logging.Debug(json);

                Sync(async () => {
                    if (json == null || Tab == null) {
                        Logging.Warning("Tab=null!");
                        return;
                    }

                    var ids = JArray.Parse(json).ToObject<string[]>();
                    Logging.Debug(ids.JoinToString("; "));

                    var i = 0;
                    using (var waiting = new WaitingDialog()) {
                        waiting.Report(0);

                        foreach (var id in ids.NonNull()) {
                            var car = await CarsManager.Instance.GetByIdAsync(id);
                            if (car == null) {
                                Logging.Warning($"Car not found: {id}");
                                continue;
                            }

                            Logging.Debug($"Car found: {car}");
                            await car.SkinsManager.EnsureLoadedAsync();

                            var skins = car.EnabledOnlySkins.ToList();
                            var skin = skins.FirstOrDefault();
                            if (skin == null) continue;

                            var liveries = new StringBuilder();
                            foreach (var x in skins) {
                                liveries.Append(
                                        $@"<img data-skin-id='{HttpUtility.HtmlEncode(x.Id)}' width=24 style='margin-left:2px' src='{await
                                                Tab.GetImageUrlAsync(x.LiveryImage)}'>");
                            }

                            var code = $@"
var u = document.createElement('div');
u.setAttribute('id', {JsonConvert.SerializeObject(id)});
u.src = {JsonConvert.SerializeObject(skin.Location)};
document.body.appendChild(u);
document.querySelector('[id^=""{id}1""]').innerHTML = ""<img width=280 style='margin-right:10px' src='{await Tab.GetImageUrlAsync(skin.PreviewImage)}'>"";
document.querySelector('[id^=""{id}2""]').innerHTML = ""{HttpUtility.HtmlEncode(car.DisplayName)}<img width=48 style='margin-left:2px;margin-right:10px;margin-top:-10px;float:left' src='{await
        Tab.GetImageUrlAsync(car.LogoIcon)}'>"";
document.querySelector('[id^=""{id}3""]').innerHTML = {JsonConvert.SerializeObject(liveries.ToString())};
document.querySelector('[id^=""{id}4""]').textContent = {JsonConvert.SerializeObject(skin.Id)};

var l = document.querySelectorAll('[id^=""{id}3""] img');
for (var i = 0; i < l.length; i++){{
    l[i].addEventListener('click', function(){{
        var s = this.getAttribute('data-skin-id');
        document.querySelector('[id^=""{id}4""]').innerHTML = s;
        window.external.UpdatePreview(""{id}"", s);
    }}, false);
}}
";
                            Logging.Debug(code);
                            Tab.Execute(code);
                            waiting.Report(new AsyncProgressEntry(car.DisplayName, i++, ids.Length));
                        }
                    }
                });
            }

            [UsedImplicitly]
            public void UpdatePreview(string carId, string skinId) {
                Sync(async () => {
                    if (carId == null || skinId == null || Tab == null) return;

                    var car = await CarsManager.Instance.GetByIdAsync(carId);
                    if (car == null) return;

                    var skin = await car.SkinsManager.GetByIdAsync(skinId);
                    if (skin == null) return;

                    Tab.Execute($@"document.querySelector('[id^=""{carId}1""] img').src = '{await Tab.GetImageUrlAsync(skin.PreviewImage)}';");
                });
            }

            [UsedImplicitly]
            public void SetParam(string key, string value) {
                Sync(() => {
                    if (Model == null) return;
                    switch (key) {
                        case "REMOTE/REQUESTED_CAR":
                            Model.CarId = value;
                            break;
                        case "CAR_0/SKIN":
                            Model.CarSkinId = value;
                            break;
                        case "REMOTE/SERVER_IP":
                            if (value != Model.Server?.Ip) {
                                Model.Server = new ServerInformation(value, Model.Server?.Port, Model.Server?.PortHttp, Model.Server?.Password,
                                        Model.Server?.DisplayName);
                            }
                            break;
                        case "REMOTE/SERVER_PORT":
                            var port = FlexibleParser.TryParseInt(value);
                            if (port != Model.Server?.Port) {
                                Model.Server = new ServerInformation(Model.Server?.Ip, port, Model.Server?.PortHttp, Model.Server?.Password,
                                        Model.Server?.DisplayName);
                            }
                            break;
                        case "REMOTE/SERVER_HTTP_PORT":
                            var portHttp = FlexibleParser.TryParseInt(value);
                            if (portHttp != Model.Server?.PortHttp) {
                                Model.Server = new ServerInformation(Model.Server?.Ip, Model.Server?.Port, portHttp,
                                        Model.Server?.Password, Model.Server?.DisplayName);
                            }
                            break;
                        case "REMOTE/PASSWORD":
                            if (value != Model.Server?.Password) {
                                Model.Server = new ServerInformation(Model.Server?.Ip, Model.Server?.Port, Model.Server?.PortHttp, value,
                                        Model.Server?.DisplayName);
                            }
                            break;
                        case "REMOTE/SERVER_NAME":
                            if (value != Model.Server?.DisplayName) {
                                Model.Server = new ServerInformation(Model.Server?.Ip, Model.Server?.Port, Model.Server?.PortHttp, Model.Server?.Password,
                                        value);
                            }
                            break;
                        case "REMOTE/NAME":
                            Logging.Debug(value);
                            if (value != Model.Player?.DisplayName) {
                                Model.Player = new PlayerInformation(value, Model.Player?.Team, Model.Player?.Nationality);
                            }
                            break;
                        case "REMOTE/TEAM":
                            if (value != Model.Player?.Team) {
                                Model.Player = new PlayerInformation(Model.Player?.DisplayName, value, Model.Player?.Nationality);
                            }
                            break;
                        case "CAR_0/NATIONALITY":
                            if (value != Model.Player?.Nationality) {
                                Model.Player = new PlayerInformation(Model.Player?.DisplayName, Model.Player?.Team, value);
                            }
                            break;
                    }
                });
            }

            [UsedImplicitly]
            public void SetParams(string json) {
                Sync(() => {
                    if (Model == null) return;

                    if (json == null) {
                        Model.Reset();
                        return;
                    }

                    var obj = JObject.Parse(json);
                    Model.CarId = obj.GetStringValueOnly("REMOTE/REQUESTED_CAR");
                    Model.TrackId = obj.GetStringValueOnly("track");
                    Model.CarSkinId = obj.GetStringValueOnly("CAR_0/SKIN");

                    Model.Server = new ServerInformation(
                            obj.GetStringValueOnly("REMOTE/SERVER_IP"),
                            obj.GetIntValueOnly("REMOTE/SERVER_PORT"),
                            obj.GetIntValueOnly("REMOTE/SERVER_HTTP_PORT"),
                            obj.GetStringValueOnly("REMOTE/PASSWORD"),
                            obj.GetStringValueOnly("REMOTE/SERVER_NAME"));
                    Logging.Debug(obj.GetStringValueOnly("REMOTE/NAME"));
                    Model.Player = new PlayerInformation(
                            obj.GetStringValueOnly("REMOTE/NAME"),
                            obj.GetStringValueOnly("REMOTE/TEAM"),
                            obj.GetStringValueOnly("CAR_0/NATIONALITY"));

                    var secondsLeft = obj.GetIntValueOnly("time");
                    Model.StartTime = secondsLeft.HasValue ? DateTime.Now + TimeSpan.FromSeconds(secondsLeft.Value) : (DateTime?)null;

                    Model.QuitUrl = obj.GetStringValueOnly("quit");
                    UpdateWaitingPage();
                });
            }

            public void UpdateWaitingPage() {
                Sync(async () => {
                    if (Model == null) return;
                    Tab?.Execute($@"
document.getElementById('{Model.CarId}1').innerHTML = '<img id=""mclaren_mp412c_gt3"" src=""{
                            await Tab.GetImageUrlAsync(Model.CarSkin?.PreviewImage)}"" height=""200"">';
document.getElementById('{Model.CarId}2').innerHTML = '<img style=""margin-top:145px;float:left"" src=""{
                            await Tab.GetImageUrlAsync(Model.Car?.LogoIcon)}"" width=""48"">';
document.getElementById('{Model.CarId}3').innerHTML = '<img style=""margin-left:300px;margin-right:10px;margin-top:160px;float:left"" src=""{
                            await Tab.GetImageUrlAsync(Model.CarSkin?.LiveryImage)}"" width=""32"">';
document.getElementById('{Model.CarId}6').textContent = {
                            JsonConvert.SerializeObject(Model.Car?.DisplayName ?? @"?")};
document.getElementById('{Model.CarId}7').textContent = {
                            JsonConvert.SerializeObject(Model.Track?.Name ?? @"?")};
document.getElementById('{Model.CarId}4').innerHTML = '<img src=""{
                            await Tab.GetImageUrlAsync(Model.Track?.PreviewImage)}"" width=""355"">';
document.getElementById('{Model.CarId}5').innerHTML = '<img src=""{
                            await Tab.GetImageUrlAsync(Model.Track?.OutlineImage)}"" height=""192"">';");
                });
            }

            [UsedImplicitly]
            public string GetCarName(string carId) {
                return Sync(() => CarsManager.Instance.GetById(carId)?.DisplayName);
            }

            [UsedImplicitly]
            public bool ContentExists(string trackId, string carIdsJson) {
                return Sync(() => TracksManager.Instance.GetLayoutByKunosId(trackId) != null &&
                        JArray.Parse(carIdsJson).Select(x => x?.ToString() ?? "").All(x => CarsManager.Instance.GetWrapperById(x) != null));
            }

            public void Go() {
                Sync(() => {
                    if (Model == null) return;
                    Model.Go().Forget();
                });
            }
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

                /*if (Ip == null) {
                    Ip = "85.114.140.51";
                    Port = 9601;
                    PortHttp = 8082;
                }*/
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

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(30, "Preparing to race", "SRS").Default();

            private const string KeyShowExtensionMessage = "Srs.ExtMsg";

            [CanBeNull]
            private WebBlock _associated;

            internal ViewModel() {
                ShowExtensionMessage = ValuesStorage.Get(KeyShowExtensionMessage, true);
            }

            private bool _showExtensionMessage;

            public bool ShowExtensionMessage {
                get => InternalUtils.IsAllRight && _showExtensionMessage;
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

            public string StartPage => SteamIdHelper.Instance.IsReady ? @"https://www.simracingsystem.com/race4.php" : null;

            public void Reset() {
                Server = null;
                Player = null;
                CarId = null;
                CarSkin = null;
                TrackId = null;
                StartTime = null;
                QuitUrl = null;
            }

            private string _quitUrl;

            public string QuitUrl {
                get => _quitUrl;
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

            [CanBeNull]
            public PlayerInformation Player {
                get => _player;
                set {
                    if (Equals(value, _player)) return;
                    Logging.Debug(value?.DisplayName);
                    _player = value;
                    OnPropertyChanged();
                    _goCommand?.RaiseCanExecuteChanged();
                    Update();
                }
            }

            private ServerInformation _server;

            [CanBeNull]
            public ServerInformation Server {
                get => _server;
                set {
                    if (Equals(value, _server)) return;
                    _server = value;
                    OnPropertyChanged();
                    _goCommand?.RaiseCanExecuteChanged();
                    Update();
                }
            }

            private bool _available;

            private void Update() {
                var available = CanGo();
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
                get => _startTime;
                set {
                    if (Equals(value, _startTime)) return;
                    _startTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LeftTime));
                }
            }

            private TimeSpan? _previousLeftTime;

            public TimeSpan? LeftTime => !_startTime.HasValue ? null : _startTime > DateTime.Now ? _startTime - DateTime.Now : TimeSpan.Zero;

            public void OnTick() {
                var leftTime = LeftTime;

                if (leftTime.HasValue) {
                    if (_previousLeftTime.HasValue && leftTime.Value.TotalMinutes < 3 && _previousLeftTime.Value.TotalMinutes >= 3) {
                        _associated?.MainTab?.RefreshCommand.Execute(null);
                    }

                    _previousLeftTime = leftTime;
                    OnPropertyChanged(nameof(LeftTime));
                }
            }

            private string _carId;

            [CanBeNull]
            public string CarId {
                get => _carId;
                set {
                    if (Equals(value, _carId)) return;
                    _carId = value;
                    OnPropertyChanged();

                    Car = string.IsNullOrWhiteSpace(value) ? null : CarsManager.Instance.GetById(value);
                    _goCommand?.RaiseCanExecuteChanged();

                    CarSkin = CarSkinId == null || Car == null ? null : Car.GetSkinById(CarSkinId);
                    Update();
                }
            }

            [CanBeNull]
            public CarObject Car {
                get => _car;
                set {
                    if (Equals(value, _car)) return;
                    _car = value;
                    OnPropertyChanged();
                    _discordPresence?.Car(value);
                }
            }

            private string _carSkinId;

            public string CarSkinId {
                get => _carSkinId;
                set {
                    if (Equals(value, _carSkinId)) return;
                    _carSkinId = value;
                    OnPropertyChanged();
                    CarSkin = string.IsNullOrWhiteSpace(value) || Car == null ? null : Car.GetSkinById(value);
                }
            }

            [CanBeNull]
            public CarSkinObject CarSkin {
                get => _carSkin;
                set => Apply(value, ref _carSkin);
            }

            private string _trackId;

            [CanBeNull]
            public string TrackId {
                get => _trackId;
                set {
                    if (Equals(value, _trackId)) return;
                    _trackId = value;
                    OnPropertyChanged();
                    Track = string.IsNullOrWhiteSpace(value) ? null : TracksManager.Instance.GetLayoutByKunosId(value);
                }
            }

            [CanBeNull]
            public TrackObjectBase Track {
                get => _track;
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();
                    _discordPresence?.Track(value);
                }
            }

            public async Task<bool> Go() {
                if (Server?.Ip == null || Player == null || CarId == null) return false;

                try {
                    if (Car == null) {
                        throw new InformativeException(string.Format(ToolsStrings.AcError_CarIsMissing, CarId), AppStrings.Srs_CarIsMissing_Commentary);
                    }

                    var anyTrack = TracksManager.Instance.GetDefault();
                    Logging.Debug(CarSkinId);
                    await GameWrapper.StartAsync(new Game.StartProperties {
                        BasicProperties = new Game.BasicProperties {
                            CarId = CarId,
                            CarSkinId = CarSkinId,
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
                                Name = Player.DisplayName ?? SrsMark.GetName(),
                                Team = Player.Team ?? "",
                                Nationality = Player.Nationality ?? ""
                            }
                        }
                    });

                    return true;
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Common_CannotStartRace, e);
                    return false;
                }
            }

            private bool CanGo() {
                return _server?.Ip != null && _server.Port.HasValue && _player != null && _carId != null;
            }

            private CommandBase _goCommand;
            private CarObject _car;
            private CarSkinObject _carSkin;
            private TrackObjectBase _track;

            public ICommand GoCommand => _goCommand ?? (_goCommand = new AsyncCommand(Go, CanGo));

            public void SetTab(WebBlock webBlock) {
                _associated = webBlock;
            }

            public void Dispose() {
                _discordPresence?.Dispose();
            }
        }

        private CommandBase _quitCommand;

        public ICommand QuitCommand => _quitCommand ?? (_quitCommand = new AsyncCommand(async () => {
            WebBrowser.MainTab?.Navigate(Model.QuitUrl);
            await Task.Delay(500);
        }, () => Model.CanQuit));

        private CommandBase _testCommand;

        public ICommand TestCommand => _testCommand ?? (_testCommand = new DelegateCommand(() => {
            WebBrowser.MainTab?.Execute(@"location.reload(true)");
        }));

        private void SrsCommon() {
            WebBrowser.MainTab?.Execute($@"
/* Set user Steam ID */
var g = document.getElementById('gui');
if (g){{
    g.innerHTML = {JsonConvert.SerializeObject(SteamIdHelper.Instance.Value ?? "")};
}} else {{
    window.external.Log('Nothing to set GUID to (' + location + ')');
}}

/* Modify labels */
var t = document.querySelector('#shoutbox input.text');
if (t){{
    t.setAttribute('placeholder', 'Join the chat');
}}", true);
        }

        private void SrsMainOld() {
            /* Old SRS (let’s keep it for now just in case) */
            WebBrowser.MainTab?.Execute(@"
/* User is not registered to any race yet, hide params */
var b = document.querySelector('[onclick*=""REMOTE/REQUESTED_CAR""]');
if (!b){
    window.external.SetParams(null);
    return;
}

/* Set next race params */
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

        private void SrsMain() {
            /* Updated SRS (21/11/2016) */
            WebBrowser.MainTab?.Execute(@"
/* Test if content is available and fix register buttons in events list */
[].forEach.call(document.querySelectorAll('input[id^=""btn""][value=""""]'), function(e){
    var s = e.parentNode.childNodes[[].indexOf.call(e.parentNode.childNodes, e) + 1].innerHTML;
    var t = /top\.__AC\.findTrack\('([^']+)'/.test(s) && RegExp.$1 || null;
    var c = []; s.replace(/top\.__AC\.findCar\('([^']+)'/g, function(_, i){ c.push(i); });
    e.value = window.external.ContentExists(t, JSON.stringify(c)) ? 'Register' : 'Missing';
});

/* Set next race params */
var o = {}, found = false;

/* Take quit URL from its button */
try {
    var quitButton = document.querySelector('input[onclick*=""unregsrs.php""]');
    var quitUrl = location.host + '/' + quitButton.getAttribute('onclick').match(/'(?:\.\/)?([^']*unregsrs[^']+)'/)[1];
    quitButton.onclick = function(){ location = '//' + quitUrl; };
    o['quit'] = quitUrl;
} catch(e){}

/* Go through every script tag and analyze stuff */
[].forEach.call(document.querySelectorAll('script'), function(e){
    var s = e.innerHTML;
    if (s.indexOf('top.__AC.findTrack(') !== -1){
        o['track'] = /top\.__AC\.findTrack\('([^']+)'/.test(s) ? RegExp.$1 : null;
        o['car'] = /top\.__AC\.Cars\.(\w+)/.test(s) ? RegExp.$1 : null;
    }

    if (s.indexOf('new Countdown(') !== -1 && /\s+time:(\d+),/.test(s)){
        o['time'] = +RegExp.$1;
    }

    if (/\$\('#mainbuttondiv'\).load\('([^']+)'/.test(s) && window.$){
        $.ajax(RegExp.$1).done(function(r){
            r.replace(/\/\/setsetting\/race\?(\w+\/\w+)=([^']*)/g, function(_, k, v){ o[k] = v == '' ? null : v; });
            window.external.SetParams(JSON.stringify(o));
        });
        found = true;
    }
});

if (!found){
    window.external.SetParams(null);
}

/* Catch all $.get requests */
if (window.$){
    if (!$._get_orig) $._get_orig = $.get;
    $.get = function(p){
        var s = p.split('?');
        switch (s[0]){
            case 'ac://start/':
                window.external.Go();
                break;
            case 'ac://setsetting/race':
                if (/^(\w+\/\w+)=([\s\S]*)$/.test(s[1])){
                    window.external.SetParam(RegExp.$1, RegExp.$2);
                }
                break;
            default:
                $._get_orig.apply($, arguments);
                break;
        }
    };
}

/* Modify car’s block until data will arrive (outside) */
if (o['car']){
    var e = document.querySelector('#' + o['car'] + '4');
    if (e) e.textContent = 'Please, wait…';
}

/* Set car names */
/*[].forEach.call(document.querySelectorAll('#regdriversupdate td:nth-child(3)'), function(e){
    e.textContent = window.external.GetCarName(e.textContent.trim());
});*/", true);
        }

        private void SrsSelectCar() {
            Logging.Debug("Preparing…");
            WebBrowser.MainTab?.Execute(@"
/* Fix cars list */
var a = [];
var b = document.querySelectorAll('[onclick*=""./regsrs.php?""]');
for (var i = 0; i < b.length; i++){ var c = (b[i].getAttribute('onclick').match(/&h=(\w+)/)||{})[1]; if (c) a.push(c); }
window.external.SetCars(JSON.stringify(a));", true);
            Logging.Debug("Ready to select car");
        }

        private async void SrsUnregister() {
            await Task.Delay(300);
            WebBrowser.MainTab?.Navigate(Model.StartPage);
            Model.Reset();
        }

        private class StyleProvider : ICustomStyleProvider {
            private string GetCustomStyle() {
                var color = AppAppearanceManager.Instance.AccentColor;
                return BinaryResources.SrsStyle
                                      .Replace(@"#E20035", color.ToHexString())
                                      .Replace(@"#CA0030", ColorExtension.FromHsb(color.GetHue(), color.GetSaturation(), color.GetBrightness() * 0.92).ToHexString());
            }

            public string GetStyle(string url, bool transparentBackgroundSupported) {
                return SettingsHolder.Live.SrsCustomStyle && url.StartsWith(@"https://www.simracingsystem.com") ?
                        GetCustomStyle() : null;
            }
        }

        private void OnPageLoaded(object sender, WebTabEventArgs e) {
            var uri = e.Tab.LoadedUrl;
            if (uri == null) return;

            SrsCommon();

            var query = Regex.Match(uri, @"/(\w+?)\d*\.php", RegexOptions.IgnoreCase);
            var page = query.Success ? query.Groups[1].Value.ToLowerInvariant() : null;

            switch (page) {
                case "select":
                    SrsSelectCar();
                    break;

                case "unregsrs":
                    SrsUnregister();
                    break;

                case "race":
                    SrsMain();
                    break;
            }
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
