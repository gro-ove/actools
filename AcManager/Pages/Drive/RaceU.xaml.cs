using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.CustomShowroom;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Drive {
    public partial class RaceU {
        private static readonly Uri RaceUUri = new Uri("/Pages/Drive/RaceU.xaml", UriKind.Relative);

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

        public RaceU() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() { }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class RaceUApiBridge : AcCompatibleApiBridge {
            public RaceUApiBridge() {
                AcApiHosts.Add(@"raceuapp-env.eba-28q3ic5a.eu-north-1.elasticbeanstalk.com");
                AcApiHosts.Add(@"localhost:3000");
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (AcApiHosts.Contains(url.GetDomainNameFromUrl(), StringComparer.OrdinalIgnoreCase)) {
                    toInject.Add(@"<script>window.AC = window.external;</script>");
                }
            }

            // RaceU API, v1
            // ReSharper disable InconsistentNaming

            public string cmVersion() {
                return BuildInformation.AppVersion;
            }

            public string getSteamId() {
                return SteamIdHelper.Instance.Value;
            }

            public void setCurrentCar(string carId, string skinId = null) {
                // TODO
            }

            public void setCurrentTrack(string trackId, string layoutId = null) {
                // TODO
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

            // ReSharper restore InconsistentNaming
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            ((WebBlock)sender).SetJsBridge<RaceUApiBridge>();
        }
    }
}