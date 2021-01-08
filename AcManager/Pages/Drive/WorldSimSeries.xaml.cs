using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.CustomShowroom;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Drive {
    public partial class WorldSimSeries : ILoadableContent {
        private static readonly Uri WorldSimSeriesUri = new Uri("/Pages/Drive/WorldSimSeries.xaml", UriKind.Relative);
        private static string _loginToken;

        public static void NavigateTo(string loginToken = null) {
            _loginToken = loginToken ?? _loginToken;
            switch (Application.Current?.MainWindow) {
                case MainWindow mainWindow:
                    mainWindow.NavigateTo(WorldSimSeriesUri);
                    break;
                case null:
                    MainWindow.NavigateOnOpen(WorldSimSeriesUri);
                    break;
            }
        }

        public WorldSimSeries() {
            this.SetCustomAccentColor(Color.FromArgb(255, 222, 235, 0));
        }

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

        public class ViewModel : NotifyPropertyChanged { }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class WorldSimSeriesApiProxy : JsProxyBase {
            public WorldSimSeriesApiProxy(JsBridgeBase bridge) : base(bridge) { }

            [CanBeNull]
            private CarObject _car;

            [CanBeNull]
            private CarSkinObject _carSkin;

            [CanBeNull]
            private TrackObjectBase _track;

            // WorldSimSeries API, v1
            // ReSharper disable InconsistentNaming

            public void showToast(string title, string message, IJavascriptCallback callback = null) {
                Sync(() => {
                    if (callback != null) {
                        Toast.Show(title, message, () => callback.ExecuteAsync());
                    } else {
                        Toast.Show(title, message);
                    }
                });
            }

            public string cmVersion() {
                return BuildInformation.AppVersion;
            }

            public string getSteamId() {
                return Sync(() => SteamIdHelper.Instance.Value);
            }

            public string getCarDataChecksum(string carId) {
                return GetAcChecksum(CarsManager.Instance.GetById(carId)?.Location, @"data.acd");
            }

            public string getTrackFileChecksum(string trackId, string layoutId, string fileName) {
                return GetAcChecksum(TracksManager.Instance.GetLayoutById(trackId, layoutId)?.DataDirectory, FileUtils.NormalizePath(fileName));
            }

            public string getCarGeneralFileChecksum(string carId, string fileName) {
                return GetAcChecksum(CarsManager.Instance.GetById(carId)?.Location, FileUtils.NormalizePath(fileName));
            }

            public string getTrackGeneralFileChecksum(string trackId, string layoutId, string fileName) {
                return GetAcChecksum(TracksManager.Instance.GetLayoutById(trackId, layoutId)?.Location, FileUtils.NormalizePath(fileName));
            }

            public void getCarDataChecksumAsync(string carId, IJavascriptCallback callback = null) {
                Task.Run(() => {
                    var checksum = GetAcChecksum(CarsManager.Instance.GetById(carId)?.Location, @"data.acd");
                    callback?.ExecuteAsync(checksum);
                }).Ignore();
            }

            public void getCarGeneralFileChecksumAsync(string carId, string fileName, IJavascriptCallback callback = null) {
                Task.Run(() => {
                    var checksum = GetAcChecksum(CarsManager.Instance.GetById(carId)?.Location, fileName);
                    callback?.ExecuteAsync(checksum);
                }).Ignore();
            }

            public void getTrackFileChecksumAsync(string trackId, string layoutId, string fileName, IJavascriptCallback callback = null) {
                Task.Run(() => {
                    var checksum = GetAcChecksum(TracksManager.Instance.GetLayoutById(trackId, layoutId)?.DataDirectory, FileUtils.NormalizePath(fileName));
                    callback?.ExecuteAsync(checksum);
                }).Ignore();
            }

            public void getTrackGeneralFileChecksumAsync(string trackId, string layoutId, string fileName, IJavascriptCallback callback = null) {
                Task.Run(() => {
                    var checksum = GetAcChecksum(TracksManager.Instance.GetLayoutById(trackId, layoutId)?.Location, FileUtils.NormalizePath(fileName));
                    callback?.ExecuteAsync(checksum);
                }).Ignore();
            }

            public bool setCurrentCar(string carId, string skinId = null) {
                return Sync(() => {
                    _car = CarsManager.Instance.GetById(carId);
                    _carSkin = skinId != null ? _car?.GetSkinById(skinId) : null;
                    return _car != null;
                });
            }

            public bool setCurrentTrack(string trackId, string layoutId = null) {
                return Sync(() => {
                    _track = TracksManager.Instance.GetLayoutById(trackId, layoutId);
                    return _track != null;
                });
            }

            public void startOnlineRace(string paramsJson, IJavascriptCallback callback = null) {
                if (_car == null) {
                    throw new Exception("Car is not set");
                }

                if (_track == null) {
                    throw new Exception("Track is not set");
                }

                var obj = JObject.Parse(paramsJson);

                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var result = await GameWrapper.StartAsync(new Game.StartProperties {
                        BasicProperties = new Game.BasicProperties {
                            CarId = _car.Id,
                            TrackId = _track.MainTrackObject.Id,
                            TrackConfigurationId = _track.LayoutId,
                            CarSkinId = _carSkin?.Id ?? _car.SelectedSkin?.Id ?? ""
                        },
                        ModeProperties = new Game.OnlineProperties {
                            Guid = SteamIdHelper.Instance.Value,
                            ServerIp = obj.GetStringValueOnly("ip") ?? throw new Exception(@"Field “ip” is required"),
                            ServerPort = obj.GetIntValueOnly("port") ?? throw new Exception(@"Field “port” is required"),
                            ServerHttpPort = obj.GetIntValueOnly("httpPort") ?? throw new Exception(@"Field “httpPort” is required"),
                            RequestedCar = _car.Id,
                            Password = obj.GetStringValueOnly("password")
                        },
                        AdditionalPropertieses = {
                            new WorldSimSeriesMark {
                                Name = obj.GetStringValueOnly("driverName"),
                                Nationality = obj.GetStringValueOnly("driverNationality"),
                                NationCode = obj.GetStringValueOnly("driverNationCode"),
                                Team = obj.GetStringValueOnly("driverTeam"),
                            }
                        }
                    });
                    callback?.ExecuteAsync(result?.IsNotCancelled);
                }).Ignore();
            }

            public bool executeCommand(string command) {
                if (command.IsAnyUrl()) {
                    Sync(() => ArgumentsHandler.ProcessArguments(new[] { command }, true).Ignore());
                    return true;
                }
                return false;
            }

            public bool openWebPage(string url) {
                if (url.IsWebUrl()) {
                    Sync(() => WindowsHelper.ViewInBrowser(url));
                    return true;
                }
                return false;
            }

            public bool openDlcWebPage(string carId) {
                return Sync(() => {
                    var car = CarsManager.Instance.GetById(carId);
                    if (car?.Dlc == null) {
                        return false;
                    }

                    WindowsHelper.ViewInBrowser(car.Dlc.Url);
                    return true;
                });
            }

            public bool launchShowroom(string carId, string skinId = null) {
                return Sync(() => {
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
                });
            }

            public void installPiece(string url) {
                Sync(() => ContentInstallationManager.Instance.InstallAsync(url, new ContentInstallationParams(false)));
            }

            public bool isCarAvailable(string carId) {
                return Sync(() => CarsManager.Instance.GetById(carId) != null);
            }

            public bool isCarSkinAvailable(string carId, string skinId) {
                return Sync(() => CarsManager.Instance.GetById(carId)?.GetSkinById(skinId) != null);
            }

            public bool isTrackAvailable(string trackId, string layoutId = null) {
                return Sync(() => TracksManager.Instance.GetLayoutById(trackId, layoutId) != null);
            }

            // ReSharper restore InconsistentNaming
        }

        public class WorldSimSeriesApiBridge : JsBridgeBase {
            public WorldSimSeriesApiBridge() {
                AcApiHosts.Add(@"worldsimseries.com");
                AcApiHosts.Add(@"paddock.worldsimseries.com");
                AcApiHosts.Add(@"local.wss:8000");
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (IsHostAllowed(url)) {
                    toInject.Add(@"<script>window.AC = window.external;</script>");
                }
            }

            protected override JsProxyBase MakeProxy() {
                return new WorldSimSeriesApiProxy(this);
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            var browser = (WebBlock)sender;
            if (_loginToken != null) {
                browser.Tabs.Clear();
                browser.OpenNewTab($@"https://paddock.worldsimseries.com/login-cm?token={_loginToken}");
                _loginToken = null;
            }

            browser.SetJsBridge<WorldSimSeriesApiBridge>();
        }
    }
}