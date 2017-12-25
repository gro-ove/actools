using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Starters {
    public class SidePassageStarter : StarterBase {
        private static readonly string Version = @"1.0.3.64";

        private static string LauncherFilename => AcPaths.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);
        private static string LauncherOriginalFilename => Path.Combine(AcRootDirectory.Instance.RequireValue, "AssettoCorsa_original.exe");
        private static string BackgroundFlagFilename => Path.Combine(AcRootDirectory.Instance.RequireValue, "AssettoCorsa_background.flag");

        private static void InstallSidePassage() {
            var launcher = LauncherFilename;
            bool updateRequired;

            if (File.Exists(launcher)) {
                var versionInfo = FileVersionInfo.GetVersionInfo(launcher);

                var alreadyInstalled = versionInfo.ProductName == "AcTools.SidePassage";
                if (alreadyInstalled) {
                    updateRequired = Version.IsVersionNewerThan(versionInfo.FileVersion ?? "");
                    if (!updateRequired) {
                        Logging.Debug("Actual version is already installed");
                        return;
                    }

                    Logging.Write($"Service is obsolete (installed: {versionInfo.FileVersion ?? "<NULL>"}, available: {Version}), update is required…");
                } else {
                    updateRequired = false;
                }

                if (!alreadyInstalled) {
                    var backup = LauncherOriginalFilename;
                    if (File.Exists(backup)) {
                        File.Move(backup, FileUtils.EnsureUnique(backup));
                    }

                    File.Move(launcher, backup);
                }
            } else {
                updateRequired = false;
            }

            try {
                if (updateRequired) {
                    try {
                        ConnectToSidePassage()?.Stop();
                    } catch (Exception e) {
                        Logging.Error(e.Message);
                    }

                    Thread.Sleep(300);
                    foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcher))) {
                        try {
                            process.Kill();
                        } catch (Exception e) {
                            Logging.Error(e.Message);
                        }
                    }

                    if (File.Exists(launcher)) {
                        File.Delete(launcher);
                    }
                }

                using (var stream = new MemoryStream(BinaryResources.SidePassage))
                using (var archive = new ZipArchive(stream)) {
                    archive.ExtractToDirectory(AcRootDirectory.Instance.RequireValue);
                }

                if (updateRequired) {
                    Toast.Show("AC Service updated", $"New version: {Version}");
                } else {
                    Toast.Show("AC Service installed", $"Original launcher renamed as “{Path.GetFileName(LauncherOriginalFilename)}”");
                }
            } catch (Exception e) {
                if (updateRequired) {
                    throw new InformativeException("Can’t update AC Service",
                            "Please, make sure it’s not running and if it is, stop it", e);
                }

                throw new InformativeException("Can’t install AC Service",
                        "Please, make sure the original launcher is not running and if it is, stop it", e);
            }

            Logging.Write("Side Passage service installed");
        }

        public static void UninstallSidePassage() {
            var backup = LauncherOriginalFilename;
            if (File.Exists(backup)) {
                var target = LauncherFilename;
                var versionInfo = FileVersionInfo.GetVersionInfo(target);

                if (versionInfo.ProductName == "AcTools.SidePassage") {
                    FileUtils.Recycle(target);
                    File.Move(backup, target);
                }
            }
        }

        [ServiceContract]
        public interface IAcSidePassage {
            [OperationContract]
            string GetSteamId();

            [OperationContract]
            void Start(string exeName);

            [OperationContract]
            string GetAchievements();

            [OperationContract]
            void Stop();
        }

        private static IAcSidePassage ConnectToSidePassage() {
            var pipeFactory = new ChannelFactory<IAcSidePassage>(new NetNamedPipeBinding(),
                    new EndpointAddress("net.pipe://localhost/AcTools.SidePassage/SidePassage"));
            var pipeProxy = pipeFactory.CreateChannel();
            Console.WriteLine("Steam ID: " + pipeProxy.GetSteamId());
            return pipeProxy;
        }

        private static bool _listeningForExit;

        private static void StartSidePassage() {
            Logging.Write("Starting Side Passage service…");
            File.WriteAllBytes(BackgroundFlagFilename, new byte[0]);

            var process = Process.Start(LauncherFilename);
            if (process == null) {
                throw new InformativeException("Can’t start side passage",
                        "Please, make sure AC Service (replacement for AssettoCorsa.exe — don’t worry, original file is renamed to AssettoCorsa_original.exe, and you can always restore it) is installed well.");
            }

            if (!_listeningForExit) {
                _listeningForExit = true;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            }
        }

        private static void OnProcessExit(object sender, EventArgs e) {
            if (SettingsHolder.Drive.AcServiceStopAtExit) {
                try {
                    ConnectToSidePassage().Stop();
                } catch (Exception) {
                    // ignored
                }
            }
        }

        [CanBeNull]
        private static IAcSidePassage EstablishConnection() {
            for (var i = 0; i < 20; i++) {
                try {
                    return ConnectToSidePassage();
                } catch (EndpointNotFoundException) {
                    if (i == 0) {
                        StartSidePassage();
                    }

                    Logging.Write($"Endpoint not found (attempt #{i + 1})");
                    Thread.Sleep(500);
                }
            }

            return null;
        }

        [ItemCanBeNull]
        private static async Task<IAcSidePassage> EstablishConnectionAsync(CancellationToken cancellation) {
            for (var i = 0; i < 120; i++) {
                if (cancellation.IsCancellationRequested) return null;

                try {
                    return ConnectToSidePassage();
                } catch (EndpointNotFoundException) {
                    if (i == 0) {
                        StartSidePassage();
                    }

                    Logging.Write($"Endpoint not found (attempt #{i + 1})");
                    await Task.Delay(500, cancellation);
                }
            }

            return null;
        }

        public override void Run() {
            SteamRunningHelper.EnsureSteamIsRunning(RunSteamIfNeeded, false);
            InstallSidePassage();

            var passage = EstablishConnection();
            if (passage == null) {
                throw new InformativeException("Can’t connect to side passage",
                        "Please, make sure AC Service  (replacement for AssettoCorsa.exe — don’t worry, original file is renamed to AssettoCorsa_original.exe, and you can always restore it) is running well or switch starters.");
            }

            passage.Start(AcsName);
        }

        public override async Task RunAsync(CancellationToken cancellation) {
            await Task.Run(() => SteamRunningHelper.EnsureSteamIsRunning(RunSteamIfNeeded, false));

            new IniFile(AcPaths.GetRaceIniFilename()) {
                ["AUTOSPAWN"] = {
                    ["ACTIVE"] = true,
                    ["__CM_SERVICE"] = true
                }
            }.Save();

            await Task.Run(() => InstallSidePassage());
            if (cancellation.IsCancellationRequested) return;

            var passage = await EstablishConnectionAsync(cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (passage == null) {
                throw new InformativeException("Can’t connect to side passage", "Please, make sure AC Service is running well or switch starters.");
            }

            passage.Start(AcsName);
        }

        public override void CleanUp() {
            if (File.Exists(BackgroundFlagFilename)) {
                File.Delete(BackgroundFlagFilename);
            }

            new IniFile(AcPaths.GetRaceIniFilename()) {
                ["AUTOSPAWN"] = {
                    ["ACTIVE"] = IniFile.Nothing,
                    ["__CM_SERVICE"] = IniFile.Nothing
                }
            }.Save();

            base.CleanUp();
        }

        [ItemCanBeNull]
        public static async Task<string> GetAchievementsAsync(CancellationToken cancellation = default(CancellationToken)) {
            await Task.Run(() => InstallSidePassage());
            if (cancellation.IsCancellationRequested) return null;

            var passage = await EstablishConnectionAsync(cancellation);
            if (cancellation.IsCancellationRequested) return null;

            if (passage == null) {
                throw new InformativeException("Can’t connect to side passage", "Please, make sure AC Service is running well or switch starters.");
            }

            return await Task.Run(() => passage.GetAchievements());
        }
    }
}