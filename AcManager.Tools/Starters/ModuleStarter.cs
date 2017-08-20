using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Starters {
    public class ModuleStarter : StarterBase {
        private static string LauncherFilename => FileUtils.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);

        private static string BackdoorFilename => Path.Combine(FileUtils.GetDocumentsDirectory(), @"launcherdata", @"filestore", @"cmhelper.ini");

        public static bool IsAssettoCorsaRunning => Process.GetProcessesByName("AssettoCorsa").Any();

        public static bool IsAvailable() {
            return IsAssettoCorsaRunning;
        }

        public static bool TryToInstallModule() {
            try {
                return InstallModule();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t install UI module", e);
                return false;
            }
        }

        /// <summary>
        /// Gets data throught backdoor INI-file. Takes about two seconds.
        /// </summary>
        /// <param name="data">Data ID.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <exception cref="InformativeException">Thrown if data is unobtainable: AC is not running or connection is failed.</exception>
        /// <returns>Module answer</returns>
        [ItemCanBeNull]
        public static async Task<string> GetDataAsync([Localizable(false),NotNull] string data, CancellationToken cancellation = default(CancellationToken)) {
            if (!IsAssettoCorsaRunning) {
                TryToRunAssettoCorsa();
                throw new InformativeException("Running AssettoCorsa.exe is required", "You’re using Module Starter, it works only though original launcher.");
            }

            var backdoor = BackdoorFilename;
            if (!File.Exists(backdoor)) {
                throw new InformativeException("Connection file is missing", "Make sure UI module is working properly.");
            }

            IniFile.Write(backdoor, "COMMAND", "CURRENT", JsonConvert.SerializeObject(new {
                name = data
            }).ToBase64());

            await Task.Delay(2000, cancellation);
            if (cancellation.IsCancellationRequested) return null;

            var ini = new IniFile(backdoor);
            var result = ini["RESULT"].GetNonEmpty(data.ToUpperInvariant());
            if (ini["COMMAND"].GetNonEmpty("CURRENT") != null || result == null) {
                throw new InformativeException("UI module does not respond", "Make sure UI module is working properly.");
            }

            return Convert.FromBase64String(result).ToUtf8String();
        }

        private const string ModuleId = "CmHelper";

        private static bool InstallModule() {
            try {
                var ini = new IniFile(Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "launcher.ini"));
                var theme = ini["WINDOW"].GetNonEmpty("theme");
                var directory = Path.Combine(AcRootDirectory.Instance.RequireValue, @"launcher", @"themes", theme ?? @"default", @"modules", ModuleId);

                var installed = false;
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);

                    using (var stream = new MemoryStream(BinaryResources.ModuleCmHelper))
                    using (var archive = new ZipArchive(stream)) {
                        archive.ExtractToDirectory(directory);
                    }

                    installed = true;
                }

                var active = ini["MODULES"].GetStrings("ACTIVE");
                if (!active.Contains(ModuleId)) {
                    ini["MODULES"].Set("ACTIVE", active.Append(@"CmHelper").Distinct());
                    ini.Save();
                    installed = true;
                }

                return installed;
            } catch (Exception e) {
                throw new InformativeException("Can’t install UI module", e);
            }
        }

        private static void TryToRunAssettoCorsa() {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = LauncherFilename,
                    WorkingDirectory = AcRootDirectory.Instance.RequireValue
                });
            } catch (Exception) {
                // ignored
            }
        }

        private void RunInner() {
            SteamRunningHelper.EnsureSteamIsRunning(RunSteamIfNeeded, false);
            SetAcX86Param();

            if (!IsAssettoCorsaRunning) {
                TryToRunAssettoCorsa();
                throw new InformativeException("Running AssettoCorsa.exe is required", "You’re using Module Starter, it works only though original launcher.");
            }

            var backdoor = BackdoorFilename;
            if (!File.Exists(backdoor)) {
                throw new InformativeException("Connection file is missing", "Make sure UI module is working properly.");
            }

            IniFile.Write(backdoor, "COMMAND", "CURRENT", JsonConvert.SerializeObject(new {
                name = @"start"
            }).ToBase64());
            Thread.Sleep(2000);

            if (new IniFile(backdoor)["COMMAND"].GetNonEmpty("CURRENT") != null) {
                throw new InformativeException("UI module does not respond", "Make sure UI module is working properly.");
            }
        }

        public override void Run() {
            InstallModule();
            RunInner();
        }

        public override Task RunAsync(CancellationToken cancellation) {
            InstallModule();
            return Task.Run(() => RunInner(), cancellation);
        }

        public override void CleanUp() {
            IniFile.Write(BackdoorFilename, "COMMAND", "CURRENT", "");
            base.CleanUp();
        }
    }
}