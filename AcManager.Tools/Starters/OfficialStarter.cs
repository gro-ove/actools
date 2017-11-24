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
    // Specially for cases when there is a CM instead of AssettoCorsa.exe
    internal class CmStarter : StarterBase {
        private static string LauncherFilename => AcPaths.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);

        public override void Run() {
            ProcessExtension.Start(LauncherFilename, new[] {
                "--run", AcsName
            }, new ProcessStartInfo {
                WorkingDirectory =  AcRootDirectory.Instance.RequireValue
            });
        }
    }

    public class OfficialStarter : StarterBase {
        private static string LauncherFilename => AcPaths.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);

        private enum Mode {
            DefaultMode, CmMode, AcServiceMode
        }

        private Mode CheckVersion() {
            if (!File.Exists(LauncherFilename)) {
                throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.OfficialStarter_OriginalLauncherIsMissing);
            }

            var version = FileVersionInfo.GetVersionInfo(LauncherFilename);

            if (version.FileDescription == "Content Manager") {
                return Mode.CmMode;
            }

            if (version.FileDescription == "AC Side Passage") {
                return Mode.AcServiceMode;
            }

            if (!version.FileVersion.IsVersionOlderThan(@"0.16.714")) {
                return Mode.DefaultMode;
            }

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

                return Mode.DefaultMode;
            }

            if (ModernDialog.ShowMessage(ToolsStrings.OfficialStarter_GameIsTooOld, ToolsStrings.NotSupported, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.OfficialStarter_UpdateAC);
            }

            SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.TrickyStarterType;
            throw new InformativeException(ToolsStrings.OfficialStarter_CannotRunGame, ToolsStrings.TryAgainDot);
        }

        private void RunInner() {
            SteamRunningHelper.EnsureSteamIsRunning(RunSteamIfNeeded, false);
            new IniFile(AcPaths.GetRaceIniFilename()) {
                ["AUTOSPAWN"] = {
                    ["ACTIVE"] = true,
                    ["__CM_SERVICE"] = IniFile.Nothing
                }
            }.Save();

            SetAcX86Param();
            LauncherProcess = Process.Start(new ProcessStartInfo {
                FileName = LauncherFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
        }

        public override void Run() {
            switch (CheckVersion()) {
                case Mode.DefaultMode:
                    RunInner();
                    break;
                case Mode.AcServiceMode:
                    AcsStarterFactory.PrepareCreated(new SidePassageStarter()).Run();
                    break;
                case Mode.CmMode:
                    AcsStarterFactory.PrepareCreated(new CmStarter()).Run();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override Task RunAsync(CancellationToken cancellation) {
            switch (CheckVersion()) {
                case Mode.DefaultMode:
                    return Task.Run(() => RunInner(), cancellation);
                case Mode.AcServiceMode:
                    return AcsStarterFactory.PrepareCreated(new SidePassageStarter()).RunAsync(cancellation);
                case Mode.CmMode:
                    return AcsStarterFactory.PrepareCreated(new CmStarter()).RunAsync(cancellation);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void CleanUp() {
            base.CleanUp();
            new IniFile(AcPaths.GetRaceIniFilename()) {
                ["AUTOSPAWN"] = {
                    ["ACTIVE"] = IniFile.Nothing,
                    ["__CM_SERVICE"] = IniFile.Nothing
                }
            }.Save();
        }
    }
}