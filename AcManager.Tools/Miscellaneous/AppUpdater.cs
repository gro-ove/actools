// #define FORCE_UPDATE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Internal;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public class AppUpdater : BaseUpdater {
        public static void OnFirstRun() {
            ValuesStorage.Set(KeyPreviousVersion, BuildInformation.AppVersion);
        }

        [Localizable(false)]
        private static string Branch => AppKeyHolder.IsAllRight && SettingsHolder.Common.UpdateToNontestedVersions ? "latest" : "tested";

        public static AppUpdater Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);

            PreviousVersion = ValuesStorage.GetString(KeyPreviousVersion);
            Logging.Write("Previos version: " + PreviousVersion);

            if (PreviousVersion?.IsVersionOlderThan(BuildInformation.AppVersion) != false) {
                JustUpdated = true;
                ValuesStorage.Set(KeyPreviousVersion, BuildInformation.AppVersion);
            }

            Instance = new AppUpdater();
        }

        private AppUpdater() : base(BuildInformation.AppVersion) {}

        protected override void OnCommonSettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.Common.UpdatePeriod):
                    RestartPeriodicCheck();
                    break;

                case nameof(SettingsHolder.Common.UpdateToNontestedVersions):
                    if (!SettingsHolder.Common.UpdateToNontestedVersions || UpdatePeriod == TimeSpan.Zero) return;
                    CheckAndUpdateIfNeeded().Forget();
                    break;
            }
        }

        private static string VersionFromData(string data) {
            return JsonConvert.DeserializeObject<AppManifest>(data).Version;
        }

        private bool _isSupported = true;

        public bool IsSupported {
            get => _isSupported;
            set {
                if (Equals(value, _isSupported)) return;
                _isSupported = value;
                OnPropertyChanged();
            }
        }

        public override Task CheckAndUpdateIfNeeded() {
#if !FORCE_UPDATE || !DEBUG
            if (!MainExecutingFile.IsPacked) {
                LatestError = ToolsStrings.AppUpdater_UnpackedVersionMessage;
                IsSupported = false;
                return Task.Delay(0);
            }

            if (BuildInformation.Platform != @"x86") {
                LatestError = "Non-x86 version doesn’t support auto-updating yet.";
                IsSupported = false;
                return Task.Delay(0);
            }
#endif

            return base.CheckAndUpdateIfNeeded();
        }

        protected override async Task<bool> CheckAndUpdateIfNeededInner() {
#if FORCE_UPDATE && DEBUG
            return ((await GetLatestVersion())?.IsVersionNewerThan(BuildInformation.AppVersion) == true || true) && await LoadAndPrepare();
#else
            return (await GetLatestVersion())?.IsVersionNewerThan(BuildInformation.AppVersion) == true && await LoadAndPrepare();
#endif
        }

        private async Task<string> GetLatestVersion() {
            if (IsGetting) return null;
            IsGetting = true;

            try {
                var data = await CmApiProvider.GetStringAsync($"app/manifest/{Branch}");
                if (data == null) {
                    LatestError = ToolsStrings.BaseUpdater_CannotDownloadInformation;
                    return null;
                }

                return VersionFromData(data);
            } catch (Exception e) {
                LatestError = ToolsStrings.BaseUpdater_CannotDownloadInformation;
                Logging.Warning("Cannot get app/manifest.json: " + e);
                return null;
            } finally {
                IsGetting = false;
            }
        }

        public class AppManifest {
            [JsonProperty(PropertyName = @"version")]
            public string Version;
        }

        private string _updateIsReady;

        public string UpdateIsReady {
            get => _updateIsReady;
            set {
                if (Equals(value, _updateIsReady)) return;
                _updateIsReady = value;
                OnPropertyChanged();
                _finishUpdateCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _isPreparing;

        private async Task<bool> LoadAndPrepare() {
#if !FORCE_UPDATE || !DEBUG
            if (!MainExecutingFile.IsPacked) {
                NonfatalError.Notify(ToolsStrings.AppUpdater_CannotUpdateApp, ToolsStrings.AppUpdater_UnpackedVersionMessage);
                LatestError = ToolsStrings.AppUpdater_UnpackedVersionMessage;
                return false;
            }
#endif

            if (_isPreparing) return false;
            _isPreparing = true;
            UpdateIsReady = null;

            try {
                var data = await CmApiProvider.GetDataAsync($"app/get/{Branch}");
                if (data == null) throw new InformativeException(ToolsStrings.AppUpdater_CannotLoad, ToolsStrings.Common_MakeSureInternetWorks);

                string preparedVersion = null;
                await Task.Run(() => {
                    if (File.Exists(UpdateLocation)) {
                        File.Delete(UpdateLocation);
                    }

                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        preparedVersion = VersionFromData(archive.GetEntry(@"Manifest.json").Open().ReadAsStringAndDispose());

                        archive.GetEntry(@"Content Manager.exe").ExtractToFile(UpdateLocation);
                        Logging.Write($"New version {preparedVersion} was extracted to “{UpdateLocation}”");
                    }
                });

                UpdateIsReady = preparedVersion;
                return true;
            } catch (UnauthorizedAccessException) {
                NonfatalError.Notify(ToolsStrings.AppUpdater_AccessIsDenied,
                        ToolsStrings.AppUpdater_AccessIsDenied_Commentary);
                LatestError = ToolsStrings.AppUpdater_AccessIsDenied_Short;
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.AppUpdater_CannotLoad,
                        ToolsStrings.AppUpdater_CannotLoad_Commentary, e);
                LatestError = ToolsStrings.AppUpdater_CannotLoadShort;
            } finally {
                _isPreparing = false;
            }

            return false;
        }

        private DelegateCommand _finishUpdateCommand;

        public DelegateCommand FinishUpdateCommand => _finishUpdateCommand ??
                (_finishUpdateCommand = new DelegateCommand(() => {
                    try {
                        RunUpdateExeAndExitIfExists();
                    } catch (Exception e) {
                        ModernDialog.ShowMessage(string.Format(ToolsStrings.AppUpdater_CannotUpdate_Message, e.Message.ToSentenceMember()),
                                ToolsStrings.AppUpdater_UpdateFailed, MessageBoxButton.OK);
                    }
                }, () => UpdateIsReady != null));

        private const string ExecutableExtension = ".exe";
        private const string UpdatePostfix = ".update" + ExecutableExtension;
        public static string UpdateLocation => MainExecutingFile.Location.ApartFromLast(ExecutableExtension, StringComparison.OrdinalIgnoreCase) + UpdatePostfix;

        private const string Certificate = "M2hi1jRoYhJcwMzEyMQkoLphsmdPVEiv51ZpnYae1f4GvGycWm0ebd95GRm5WRkMBA35gULMoSzMwmyOyc45pUkGcuK8hmaGRsaG" +
                "RoYGQBAlzmtsCeQaQLnYtDQxKiEbzMjKwNzEyM8AFOdiamJkZPiW1c1orPHmXIBsq+tTkQ3/BORmGG5NNjdO0lDiXMzX+jvzEGvaFyW9vfFeh3mDdE+wRDoe0GvYlBx" +
                "5K7ZDNGyZX+SWvbutbEuuX+V42nC8JULvyRxbXt87Z7/7sPA3mHCu2lVVG7ko8aCD+142iX2blq2W9zq9I3tjn8/Zjnsbkqp3Cjl3e3vlNMhMiLmVvjwsnvPlrLuOc5" +
                "o0RJPvrd4Y94OtZ1dF7aNwhyA2zxTGrpLP3qzfrzz8YHdHcaraq/0aW7/tWHPqj9SPz7Wtcw+Z3sy/fCbWIucWm3p+xOW5b49P3hI6c6LcOrWol/s2rOJ86FH69pYj1" +
                "9qYaca3F+qZt3DNvOv0YeHTnVImtUzMjAyMi2sNqg0UgAEny8LI+J9FzEDEgM+Ah41Lm42RhbHJnIlRlJmJvcFAGKRClYXHgIuNAyjFysrOzGzgBBJkZLE2sGwQOMGy" +
                "SspBfNrBNnGVjz+e68xfKIwlSpoIxTwzKF4MZNsd12q4iuvYuzikXjUwjEqyuyEU9WPzy2mbT5oI/Y/+szpQyb2j6jvv22DmSZxqh5QtmNe5RIYZV+6Z/uWB7BKjF5H" +
                "ehvMOiGSu+cHCfjBs58JGM7XapqSo1Kd8DcckEjgytvm/vv3vz3P74sUS2zX0E2aFrH/1KeOpkMO1zI978m4Ff5v1OuJd7Qc/sQ2J9SsSp6vffrDolMWCXT6zVSfH8O" +
                "szKhtG77jVIcE8o+pQiPnK6Jq0ZZKtv27dXvPub8eV8LgI3ztXXjzIP5DxRE7a8nrd999eitvfCiQUJJ3KeTJ54dyuJapM/1IKmFdO1Fdxltlc2PBt0nJJCb3fNYof4" +
                "mx6L7KGOq5PywAA";

        private static byte[] Decompress(byte[] data) {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream()) {
                using (var dstream = new DeflateStream(input, CompressionMode.Decompress)) {
                    dstream.CopyTo(output);
                }

                return output.ToArray();
            }
        }

        private static void CheckSignature(string filename) {
            var certificate = X509Certificate.CreateFromSignedFile(filename);
            if (new X509Certificate2(certificate.Handle).RawData.EqualsTo(Decompress(Convert.FromBase64String(Certificate)))) {
                throw new Exception("Signature is wrong");
            }

            var result = AuthenticodeTools.IsTrusted(filename);
            switch (result) {
                case WintrustResult.Success:
                case WintrustResult.UntrustedRoot:
                case WintrustResult.SubjectNotTrusted:
                case WintrustResult.SubjectCertificateExpired:
                case WintrustResult.SubjectCertificateRevoked:
                case WintrustResult.SubjectExplicitlyDistrusted:
                    break;
                case WintrustResult.ProviderUnknown:
                case WintrustResult.ActionUnknown:
                case WintrustResult.SubjectFormUnknown:
                case WintrustResult.FileNotSigned:
                case WintrustResult.SignatureOrFileCorrupt:
                    throw new Exception(result.GetDescription().ToSentenceMember());
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool OnStartup(string[] args) {
            try {
                if (MainExecutingFile.Location.EndsWith(UpdatePostfix)) {
                    InstallAndRunNewVersion();
                    return true;
                }

                var updateLocation = UpdateLocation;
                if (File.Exists(updateLocation)) {
                    if (FileVersionInfo.GetVersionInfo(updateLocation).FileVersion.IsVersionNewerThan(BuildInformation.AppVersion)) {
                        // TODO: Enable signature check
                        // CheckSignature(updateLocation);

                        Thread.Sleep(200);
                        RunUpdateExeAndExitIfExists();
                        return true;
                    }

                    CleanUpUpdateExeAsync().Forget();
                }

                return false;
            } catch (Exception e) {
                MessageBox.Show(string.Format(ToolsStrings.AppUpdater_CannotUpdate_Message, e.Message.ToSentenceMember()), ToolsStrings.AppUpdater_UpdateFailed,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void RunUpdateExeAndExitIfExists() {
            var updateLocation = UpdateLocation;
            if (!File.Exists(updateLocation)) return;
            CheckSignature(updateLocation);
            Logging.Write($"Starting “{updateLocation}”…");
            ProcessExtension.Start(updateLocation, Environment.GetCommandLineArgs().Skip(1));
            Environment.Exit(0);
        }

        private static async Task CleanUpUpdateExeAsync() {
            for (var i = 0; i < 10; i++) {
                try {
                    File.Delete(UpdateLocation);
                    break;
                } catch (Exception) {
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
        }

        private static void InstallAndRunNewVersion() {
            /* will be replaced this file */
            var originalFilename = MainExecutingFile.Location.ApartFromLast(UpdatePostfix, StringComparison.OrdinalIgnoreCase) + ExecutableExtension;

            /* if file already exists */
            if (File.Exists(originalFilename)) {
                /* ten attempts, five seconds */
                for (var i = 0; i < 10; i++) {
                    try {
                        File.Delete(originalFilename);
                        break;
                    } catch (Exception) {
                        Thread.Sleep(500);
                    }
                }

                /* if we couldn’t delete file normally, let’s kill any process with this name */
                if (File.Exists(originalFilename)) {
                    try {
                        foreach (var process in Process.GetProcessesByName(Path.GetFileName(originalFilename))) {
                            process.Kill();
                        }
                    } catch (Exception) {
                        // ignored
                    }

                    /* four attempts, two seconds */
                    for (var i = 0; i < 4; i++) {
                        try {
                            File.Delete(originalFilename);
                        } catch (Exception) {
                            Thread.Sleep(500);
                        }
                    }
                }

                if (File.Exists(originalFilename)) {
                    MessageBox.Show(
                            string.Format(ToolsStrings.AppUpdater_CannotUpdate_HelpNeeded, Path.GetFileName(originalFilename),
                                    Path.GetFileName(MainExecutingFile.Location)),
                            ToolsStrings.AppUpdater_UpdateFailed, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            File.Copy(MainExecutingFile.Location, originalFilename);
            ProcessExtension.Start(originalFilename, Environment.GetCommandLineArgs().Skip(1));
            Environment.Exit(0);
        }

        private const string KeyPreviousVersion = "AppUpdater:PreviousVersion";

        public static bool JustUpdated { get; private set; }

        public static string PreviousVersion { get; private set; }

        public static IEnumerable<ChangelogEntry> LoadChangelog() {
            return InternalUtils.LoadChangelog(CmApiProvider.UserAgent, true);
        }
    }
}
