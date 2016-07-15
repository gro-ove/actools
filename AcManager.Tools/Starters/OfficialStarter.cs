using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Starters {
    public class OfficialStarter : BaseStarter {
        private static string LauncherFilename => FileUtils.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);

        private void CheckVersion() {
            if (!FileVersionInfo.GetVersionInfo(LauncherFilename).FileVersion.IsVersionOlderThan("0.16.714")) return;

            if (ModernDialog.ShowMessage(
                    "Can’t use Official Starter: game is too old. Would you like to switch to Tricky Starter instead? You can always go to Settings/Drive and change it.",
                    "Not Supported", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                throw new InformativeException("Can’t run the game", "Please, update AC or change Starter to something else.");
            }

            SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
            throw new InformativeException("Can’t run the game", "Try again.");
        }

        private void RunInner() {
            IniFile.Write(FileUtils.GetRaceIniFilename(), "AUTOSPAWN", "ACTIVE", "1");
            IniFile.Write(Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "launcher.ini"), "WINDOW", "X86", Use32Version ? "1" : "0");
            LauncherProcess = Process.Start(new ProcessStartInfo {
                FileName = LauncherFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
        }

        public override void Run() {
            CheckVersion();
            RunInner();
        }

        public override Task RunAsync(CancellationToken cancellation) {
            CheckVersion();
            return Task.Run(() => RunInner(), cancellation);
        }

        public override void CleanUp() {
            IniFile.Write(FileUtils.GetRaceIniFilename(), "AUTOSPAWN", "ACTIVE", "0");
        }
    }
}