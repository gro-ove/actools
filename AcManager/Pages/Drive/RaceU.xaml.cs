using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.CustomShowroom;
using AcManager.Internal;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Drive {
    public partial class RaceU {
        private static readonly Uri RaceUUri = new Uri("/Pages/Drive/RaceU.xaml", UriKind.Relative);
        private static RaceU _instance;

        public static void NavigateTo() {
            switch (Application.Current?.MainWindow) {
                case MainWindow mainWindow:
                    mainWindow.NavigateTo(RaceUUri);
                    break;
                case null:
                    MainWindow.NavigateOnOpen(RaceUUri);
                    break;
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        private static string _navigateToNext = GetRaceUAddress(null);

        private static string GetRaceUAddress(string tag) {
            if (string.IsNullOrWhiteSpace(tag)) {
                return @"https://raceu.net";
            }
            if (tag.IsWebUrl()) {
                return tag;
            }
            return @"https://raceu.net/" + tag;
        }

        public RaceU() {
            this.SetCustomAccentColor(Color.FromArgb(255, 20, 190, 1));

            if (_instance == null && Application.Current?.MainWindow is MainWindow mainWindow) {
                mainWindow.AddHandler(ModernMenu.SelectedChangeEvent, new EventHandler<ModernMenu.SelectedChangeEventArgs>(OnMainWindowLinkChange));
            }
            _instance = this;

            var originalAccentColor = AppAppearanceManager.Instance.AccentColor;
            this.OnActualUnload(() => AppAppearanceManager.Instance.AccentColor = originalAccentColor);

            DataContext = new ViewModel();
            InitializeComponent();

            Browser.StartPage = _navigateToNext;
            Browser.CurrentTabChanged += OnCurrentTabChanged;
            Browser.Tabs.CollectionChanged += OnTabsCollectionChanged;
        }

        private WebTab _previousTab;

        private void OnCurrentTabChanged(object sender, EventArgs e) {
            _previousTab?.UnsubscribeWeak(OnCurrentTabPropertyChanged);
            _previousTab = Browser.CurrentTab;
            _previousTab?.SubscribeWeak(OnCurrentTabPropertyChanged);
            OnAddressChanged();
        }

        private void OnAddressChanged() {
            if (Application.Current?.MainWindow is MainWindow mainWindow) {
                var menu = mainWindow.FindChild<ModernMenu>("PART_Menu");
                if (menu != null) {
                    menu.SelectedLink = menu.SelectedLinkGroup?.Links.FirstOrDefault(x => GetRaceUAddress(x.Tag) == Browser.CurrentTab?.LoadedUrl)
                            ?? menu.SelectedLink;
                }
                UpdateAddressBarVisible();
            }
        }

        private void UpdateAddressBarVisible() {
            Browser.IsAddressBarVisible = Browser.Tabs.Count > 1 || Browser.Tabs.FirstOrDefault()?.LoadedUrl?.StartsWith("https://raceu.net") != true;
        }

        private void OnCurrentTabPropertyChanged(object o, PropertyChangedEventArgs a) {
            if (a.PropertyName == nameof(WebTab.LoadedUrl)) {
                OnAddressChanged();
                UpdateAddressBarVisible();
            }
        }

        private void OnTabsCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) {
            UpdateAddressBarVisible();
        }

        private void OnMainWindowLinkChange(object sender, ModernMenu.SelectedChangeEventArgs args) {
            if (args.SelectedLink?.Source == RaceUUri) {
                _navigateToNext = GetRaceUAddress(args.SelectedLink?.Tag);
                if (_instance?.Browser.Tabs.Count > 0) {
                    _instance?.Browser.Tabs[0].Navigate(_navigateToNext);
                }
            }
        }

        private Button _windowBackButton;
        private IInputElement _windowBackButtonPreviousTarget;

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = Browser.CurrentTab?.BackCommand.CanExecute(null) == true
                    || NavigationCommands.BrowseBack.CanExecute(null, _windowBackButtonPreviousTarget);
            e.Handled = true;
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            if (Browser.CurrentTab?.BackCommand.CanExecute(null) == true) {
                Browser.CurrentTab?.BackCommand.Execute(null);
            } else {
                NavigationCommands.BrowseBack.Execute(null, _windowBackButtonPreviousTarget);
            }
            e.Handled = true;
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = Browser.CurrentTab?.ForwardCommand.CanExecute(null) == true
                    || NavigationCommands.BrowseForward.CanExecute(null, _windowBackButtonPreviousTarget);
            e.Handled = true;
        }

        private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e) {
            if (Browser.CurrentTab?.ForwardCommand.CanExecute(null) == true) {
                Browser.CurrentTab?.ForwardCommand.Execute(null);
            } else {
                NavigationCommands.BrowseForward.Execute(null, _windowBackButtonPreviousTarget);
            }
            e.Handled = true;
        }

        private void Refresh_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void Refresh_Executed(object sender, ExecutedRoutedEventArgs e) {
            Browser.CurrentTab?.RefreshCommand?.Execute(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            e.Handled = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _windowBackButton = Application.Current?.MainWindow?.FindChild<Button>("WindowBackButton");
            if (_windowBackButton != null && _windowBackButtonPreviousTarget == null) {
                _windowBackButtonPreviousTarget = _windowBackButton.CommandTarget;
                _windowBackButton.CommandTarget = this;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_windowBackButton != null) {
                _windowBackButton.CommandTarget = _windowBackButtonPreviousTarget;
            }
        }

        public class ViewModel : NotifyPropertyChanged { }

        [UsedImplicitly]
        private class RaceULink {
#pragma warning disable 649
            public string Name;
            public string Url;
            public bool New;
#pragma warning restore 649
        }

        private static void SetRaceULinks(string encoded) {
            if (Application.Current?.MainWindow is MainWindow mainWindow) {
                try {
                    if (string.IsNullOrWhiteSpace(encoded)) {
                        mainWindow.UpdateRaceULinks(new Link[0]);
                    } else {
                        mainWindow.UpdateRaceULinks(JsonConvert.DeserializeObject<List<RaceULink>>(encoded).Select(x => new Link {
                            DisplayName = x.Name,
                            IsNew = x.New,
                            Source = RaceUUri,
                            Tag = x.Url
                        }));
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        public static void InitializeRaceULinks() {
            SetRaceULinks(CacheStorage.Get<string>(".raceULinks"));
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class RaceUApiProxy : JsProxyBase {
            public RaceUApiProxy(JsBridgeBase bridge) : base(bridge) { }

            [CanBeNull]
            private CarObject _car;

            [CanBeNull]
            private CarSkinObject _carSkin;

            [CanBeNull]
            private TrackObjectBase _track;

            // RaceU API, v1
            // ReSharper disable InconsistentNaming

            public void showToast(string title, string message, IJavascriptCallback callback = null) {
                if (callback != null) {
                    Toast.Show(title, message, () => callback.ExecuteAsync());
                } else {
                    Toast.Show(title, message);
                }
            }

            public void _setLinks(string encodedData) {
                CacheStorage.Set(".raceULinks", encodedData);
                ActionExtension.InvokeInMainThread(() => SetRaceULinks(encodedData));
            }

            public string cmVersion() {
                return BuildInformation.AppVersion;
            }

            public string getSteamId() {
                return SteamIdHelper.Instance.Value;
            }

            public string getCarDataChecksum(string carId) {
                return GetAcChecksum(CarsManager.Instance.GetById(carId)?.Location, @"data.acd");
            }

            public string getTrackFileChecksum(string trackId, string layoutId, string fileName) {
                return GetAcChecksum(TracksManager.Instance.GetLayoutById(trackId, layoutId)?.DataDirectory, fileName);
            }

            public bool setCurrentCar(string carId, string skinId = null) {
                _car = CarsManager.Instance.GetById(carId);
                _carSkin = skinId != null ? _car?.GetSkinById(skinId) : null;
                return _car != null;
            }

            public bool setCurrentTrack(string trackId, string layoutId = null) {
                _track = TracksManager.Instance.GetLayoutById(trackId, layoutId);
                return _track != null;
            }

            public void _startOnlineRace(string jsonParams, IJavascriptCallback callback = null) {
                var args = JObject.Parse(jsonParams);

                if (_car == null) {
                    throw new Exception("Car is not set");
                }

                if (_track == null) {
                    throw new Exception("Track is not set");
                }

                var ip = args.GetStringValueOnly("ip") ?? throw new Exception("“ip” parameter is missing");
                var port = args.GetIntValueOnly("port") ?? throw new Exception("“port” parameter is missing");

                var properties = new Game.StartProperties {
                    BasicProperties = new Game.BasicProperties {
                        CarId = _car.Id,
                        TrackId = _track.MainTrackObject.Id,
                        TrackConfigurationId = _track.LayoutId,
                        CarSkinId = _carSkin?.Id ?? _car.SelectedSkin?.Id ?? ""
                    },
                    ModeProperties = new Game.OnlineProperties {
                        Guid = SteamIdHelper.Instance.Value,
                        ServerIp = ip,
                        ServerPort = port,
                        ServerHttpPort = args.GetIntValueOnly("httpPort") ?? throw new Exception("“httpPort” parameter is missing"),
                        Password = args.GetStringValueOnly("password") ?? InternalUtils.GetRaceUPassword(_track.IdWithLayout, ip, port),
                        RequestedCar = _car.Id,
                        CspFeaturesList = args.GetStringValueOnly("cspFeatures"),
                        CspReplayClipUploadUrl = args.GetStringValueOnly("cspReplayClipUploadUrl"),
                    }
                };

                ActionExtension.InvokeInMainThread(async () => {
                    var result = await GameWrapper.StartAsync(properties);
                    callback?.ExecuteAsync(result?.IsNotCancelled);
                }).Ignore();
            }

            public bool executeCommand(string command) {
                if (command.IsAnyUrl()) {
                    ArgumentsHandler.ProcessArguments(new[] { command }, true).Ignore();
                    return true;
                }
                return false;
            }

            public bool openWebPage(string url) {
                if (url.IsWebUrl()) {
                    WindowsHelper.ViewInBrowser(url);
                    return true;
                }
                return false;
            }

            public bool openDlcWebPage(string carId) {
                var car = CarsManager.Instance.GetById(carId);
                if (car?.Dlc == null) {
                    return false;
                }

                WindowsHelper.ViewInBrowser(car.Dlc.Url);
                return true;
            }

            public bool isShadersPatchInstalled() {
                return PatchHelper.IsActive();
            }

            public bool isWeatherFxActive() {
                return PatchHelper.IsFeatureSupported(PatchHelper.FeatureFullDay);
            }

            public bool launchShowroom(string carId, string skinId = null) {
                var car = CarsManager.Instance.GetById(carId);
                if (car == null) {
                    return false;
                }

                var skin = car.SelectedSkin;
                if (skinId != null) {
                    skin = car.GetSkinById(skinId);
                    if (skin == null) {
                        return false;
                    }
                }
                CustomShowroomWrapper.StartAsync(car, skin);
                return true;
            }

            public void installPiece(string url) {
                ContentInstallationManager.Instance.InstallAsync(url, new ContentInstallationParams(false));
            }

            public bool isCarAvailable(string carId) {
                return CarsManager.Instance.GetById(carId) != null;
            }

            public bool isTrackAvailable(string trackId, string layoutId = null) {
                return TracksManager.Instance.GetLayoutById(trackId, layoutId) != null;
            }

            public bool isThemeDark() {
                return ((Color)Application.Current.Resources[@"WindowBackgroundColor"]).GetBrightness() < 0.4;
            }

            public string getThemeAccentColor() {
                return ((Color)Application.Current.Resources[@"WindowBackgroundColor"]).ToHexString();
            }

            public void setThemeAccentColor(string color) {
                ActionExtension.InvokeInMainThread(
                        () => { AppAppearanceManager.Instance.AccentColor = color.ToColor() ?? AppAppearanceManager.Instance.AccentColor; });
            }

            // ReSharper restore InconsistentNaming
        }

        public class RaceUApiBridge : JsBridgeBase {
            public RaceUApiBridge() {
                AcApiHosts.Add(@"raceu.net");
                AcApiHosts.Add(@"localhost:3000");
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (IsHostAllowed(url)) {
                    toInject.Add(@"<script>
window.AC = window.external;
window.AC.setLinks = function (links){ return window.AC._setLinks(JSON.stringify(links)); };
window.AC.startOnlineRace = function (options, callback){
    if (arguments.length >= 3) {
        options = { ip: options, port: callback, httpPort: arguments[2] };
        callback = arguments[3];
    }
    window.AC._startOnlineRace(JSON.stringify(options), callback); 
};
</script>");
                }
            }

            public override void PageHeaders(string url, IDictionary<string, string> headers) {
                base.PageHeaders(url, headers);
                if (IsHostAllowed(url)) {
                    headers[@"X-Checksum"] = InternalUtils.GetRaceUChecksum(SteamIdHelper.Instance.Value);
                }
            }

            protected override JsProxyBase MakeProxy() {
                return new RaceUApiProxy(this);
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            ((WebBlock)sender).SetJsBridge<RaceUApiBridge>();
        }
    }
}