using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Starters {
    public class OfficialStarter : BaseStarter {
        private static string LauncherFilename => FileUtils.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);

        private void CheckVersion() {
            if (!File.Exists(LauncherFilename)) {
                throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.OfficialStarter_OriginalLauncherIsMissing);
            }

            if (!FileVersionInfo.GetVersionInfo(LauncherFilename).FileVersion.IsVersionOlderThan(@"0.16.714")) return;

            if (StarterPlus.IsPatched(LauncherFilename)) {
                var backupFilename = StarterPlus.BackupFilename;
                if (!File.Exists(backupFilename)) {
                    ModernDialog.ShowMessage(ToolsStrings.OfficialStarter_DownloadAndRestore, ToolsStrings.OfficialStarter_CannotRun, MessageBoxButton.OK);
                    throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.OfficialStarter_RestoreOriginalLauncher);
                }

                try {
                    File.Delete(LauncherFilename);
                } catch (Exception) {
                    if (ModernDialog.ShowMessage(
                            ToolsStrings.OfficialStarter_RestoreLauncherManually,
                            ToolsStrings.OfficialStarter_CannotRun, MessageBoxButton.OKCancel) == MessageBoxResult.OK) {
                        WindowsHelper.ViewFile(backupFilename);
                    }
                    throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.OfficialStarter_RestoreOriginalLauncher);
                }

                File.Copy(backupFilename, LauncherFilename);
                try {
                    File.Delete(backupFilename);
                } catch (Exception) {
                    // ignored
                }

                return;
            }

            if (ModernDialog.ShowMessage(ToolsStrings.OfficialStarter_GameIsTooOld, ToolsStrings.NotSupported, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.OfficialStarter_UpdateAC);
            }

            SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
            throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.TryAgainDot);
        }

        private void RunInner() {
            IniFile.Write(FileUtils.GetRaceIniFilename(), "AUTOSPAWN", "ACTIVE", "1");
            SetAcX86Param();
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
            base.CleanUp();
            IniFile.Write(FileUtils.GetRaceIniFilename(), "AUTOSPAWN", "ACTIVE", "0");
        }
    }
}