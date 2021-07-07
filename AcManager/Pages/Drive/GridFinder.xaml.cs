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
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    public partial class GridFinder: ILoadableContent {
        private static readonly Uri GridFinderUri = new Uri("/Pages/Drive/GridFinder.xaml", UriKind.Relative);
        private static string _loginToken;

        public static void NavigateTo(string loginToken = null) {
            _loginToken = loginToken ?? _loginToken;
            switch (Application.Current?.MainWindow) {
                case MainWindow mainWindow:
                    mainWindow.NavigateTo(GridFinderUri);
                    break;
                case null:
                    MainWindow.NavigateOnOpen(GridFinderUri);
                    break;
            }
        }

        public GridFinder() {
            this.SetCustomAccentColor(Color.FromArgb(255, 253, 123, 13));
        }

        public static PluginsRequirement Requirement { get; } = new PluginsRequirement(KnownPlugins.CefSharp);

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await Task.Yield();
            // await CarsManager.Instance.EnsureLoadedAsync();
            // await TracksManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            // CarsManager.Instance.EnsureLoaded();
            // TracksManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged { }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class GridFinderApiProxy : JsProxyBase {
            public GridFinderApiProxy(JsBridgeBase bridge) : base(bridge) { }

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

        public class GridFinderApiBridge : JsBridgeBase {
            public GridFinderApiBridge() {
                AcApiHosts.Add(@"grid-finder.com");
                AcApiHosts.Add(@"www.grid-finder.com");
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (IsHostAllowed(url)) {
                    toInject.Add(@"<script>window.AC = window.external;</script>");
                }
            }

            protected override JsProxyBase MakeProxy() {
                return new GridFinderApiProxy(this);
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            var browser = (WebBlock)sender;
            browser.SetJsBridge<GridFinderApiBridge>();
        }
    }
}