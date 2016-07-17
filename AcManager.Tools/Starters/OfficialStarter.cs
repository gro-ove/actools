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
                throw new InformativeException(Resources.OfficialStarter_CannotRunGame, Resources.OfficialStarter_OriginalLauncherIsMissing);
            }

            if (!FileVersionInfo.GetVersionInfo(LauncherFilename).FileVersion.IsVersionOlderThan("0.16.714")) return;

            if (StarterPlus.IsPatched(LauncherFilename)) {
                var backupFilename = StarterPlus.BackupFilename;
                if (!File.Exists(backupFilename)) {
                    ModernDialog.ShowMessage(Resources.OfficialStarter_DownloadAndRestore, Resources.OfficialStarter_CannotRun, MessageBoxButton.OK);
                    throw new InformativeException(Resources.OfficialStarter_CannotRunGame, Resources.OfficialStarter_RestoreOriginalLauncher);
                }

                try {
                    File.Delete(LauncherFilename);
                } catch (Exception) {
                    if (ModernDialog.ShowMessage(
                            Resources.OfficialStarter_RestoreLauncherManually,
                            Resources.OfficialStarter_CannotRun, MessageBoxButton.OKCancel) == MessageBoxResult.OK) {
                        WindowsHelper.ViewFile(backupFilename);
                    }
                    throw new InformativeException(Resources.OfficialStarter_CannotRunGame, Resources.OfficialStarter_RestoreOriginalLauncher);
                }

                File.Copy(backupFilename, LauncherFilename);
                try {
                    File.Delete(backupFilename);
                } catch (Exception) {
                    // ignored
                }

                return;
            }

            if (ModernDialog.ShowMessage(Resources.OfficialStarter_GameIsTooOld, Resources.NotSupported, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                throw new InformativeException(Resources.OfficialStarter_CannotRunGame, Resources.OfficialStarter_UpdateAC);
            }

            SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
            throw new InformativeException(Resources.OfficialStarter_CannotRunGame, Resources.TryAgainDot);
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