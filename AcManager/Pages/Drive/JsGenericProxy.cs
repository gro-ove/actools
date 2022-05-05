using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.UserControls.Web;
using AcManager.CustomShowroom;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Drive {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
    public class JsGenericProxy : JsProxyBase {
        public JsGenericProxy(JsBridgeBase bridge) : base(bridge) { }

        public void showToast(string title, string message, IJavascriptCallback callback = null) {
            if (callback != null) {
                Toast.Show(title, message, () => callback.ExecuteAsync());
            } else {
                Toast.Show(title, message);
            }
        }

        public string getSteamId() {
            return SteamIdHelper.Instance.Value;
        }

        public string cmVersion() {
            return BuildInformation.AppVersion;
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
                var checksum = GetAcChecksum(CarsManager.Instance.GetById(carId)?.Location, FileUtils.NormalizePath(fileName));
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
                using (GameWrapper.SetPropertiesCallback(p => p.SetAdditional(new LiveServiceMark("Generic")))) {
                    ArgumentsHandler.ProcessArguments(new[] { command }, true).Ignore();
                }
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

        public bool isCarSkinAvailable(string carId, string skinId) {
            return Sync(() => CarsManager.Instance.GetById(carId)?.GetSkinById(skinId) != null);
        }

        public string getCarVersion(string carId) {
            return CarsManager.Instance.GetById(carId)?.Version;
        }

        public string getTrackVersion(string trackId, string layoutId = null) {
            return TracksManager.Instance.GetLayoutById(trackId, layoutId)?.Version;
        }

        public bool isThemeDark() {
            return ((Color)Application.Current.Resources[@"WindowBackgroundColor"]).GetBrightness() < 0.4;
        }

        public string getThemeAccentColor() {
            return ((Color)Application.Current.Resources[@"WindowBackgroundColor"]).ToHexString();
        }
    }
}