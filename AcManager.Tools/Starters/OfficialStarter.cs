using System;
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
            if (!File.Exists(LauncherFilename)) {
                throw new InformativeException("Can’t run the game", "Original launcher is missing; please, change starter.");
            }

            if (!FileVersionInfo.GetVersionInfo(LauncherFilename).FileVersion.IsVersionOlderThan("0.16.714")) return;

            if (StarterPlus.IsPatched(LauncherFilename)) {
                var backupFilename = StarterPlus.BackupFilename;
                if (!File.Exists(backupFilename)) {
                    ModernDialog.ShowMessage(
                            "Can’t restore non-patched version of original launcher. Please, [url=\"https://drive.google.com/file/d/0B6GfX1zRa8pOcUxlTlU2WWZZTWM/view?usp=drivesdk\"]restore it manually[/url].", "Can’t Run", MessageBoxButton.OK);
                    throw new InformativeException("Can’t run the game", "Please, restore original launcher and try again (or change starter).");
                }

                try {
                    File.Delete(LauncherFilename);
                } catch (Exception) {
                    if (ModernDialog.ShowMessage(
                            "Can’t restore non-patched version of original launcher. Please, restore it manually: you have to replace “AssettoCorsa.exe” with “AssettoCorsa_backup_sp.exe”.",
                            "Can’t Run", MessageBoxButton.OKCancel) == MessageBoxResult.OK) {
                        WindowsHelper.ViewFile(backupFilename);
                    }
                    throw new InformativeException("Can’t run the game", "Please, restore original launcher and try again (or change starter).");
                }

                File.Copy(backupFilename, LauncherFilename);
                try {
                    File.Delete(backupFilename);
                } catch (Exception) {
                    // ignored
                }

                return;
            }

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