using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Starters {
    public class StarterPlus : StarterBase, IAcsPrepareableStarter, IPluginWrapper {
        public const string AddonId = "StarterPlus";

        public string Id => AddonId;

        private static string LauncherFilename => FileUtils.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);

        public static string BackupFilename => LauncherFilename.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase) + "_backup_sp.exe";

        public static bool IsPatched(string filename) {
            return File.Exists(filename) && FileVersionInfo.GetVersionInfo(filename).FileDescription.Contains(@"(Patched for CM)");
        }

        private static bool IsPatched() {
            return AcRootDirectory.Instance.Value != null && IsPatched(LauncherFilename);
        }

        public void Enable() {
            if (AcRootDirectory.Instance == null) return;

            Logging.Warning("Patch()");
            if (IsPatched()) return;

            if (AcRootDirectory.Instance.Value == null) {
                Logging.Warning("AC Root directory is missing.");
                return;
            }

            var addon = PluginsManager.Instance.GetById(AddonId);
            if (addon?.IsReady != true) {
                Logging.Warning("Addon is not installed or enabled.");
                return;
            }

            var launcherFilename = LauncherFilename;
            var backupFilename = BackupFilename;

            if (File.Exists(launcherFilename)) {
                try {
                    if (File.Exists(backupFilename)) {
                        File.Delete(backupFilename);
                    }

                    File.Move(launcherFilename, backupFilename);
                } catch (Exception e) {
                    Logging.Warning("Can’t move original file out of the way: " + e);
                    return;
                }
            }

            var sourceFilename = addon.GetFilename("data.pak");

            try {
                using (var archive = ZipFile.OpenRead(sourceFilename)) {
                    archive.GetEntry("AssettoCorsa.exe").ExtractToFile(launcherFilename);
                }
            } catch (Exception e) {
                Logging.Warning("Can’t extract file: " + e);
                Logging.Warning("Rollback!");
                Disable();
                return;
            }

            Logging.Write("Enabled.");
        }

        public void Disable() {
            Logging.Warning("RollBack()");
            if (!IsPatched()) return;

            if (AcRootDirectory.Instance.Value == null) {
                Logging.Warning("AC Root directory is missing.");
                return;
            }

            var launcherFilename = LauncherFilename;
            var backupFilename = BackupFilename;

            if (!File.Exists(backupFilename)) {
                Logging.Warning("Backup file is missing.");
                return;
            }

            if (File.Exists(launcherFilename)) {
                try {
                    File.Delete(launcherFilename);
                } catch (Exception e) {
                    Logging.Warning("Can’t move modified file out of the way: " + e);
                    return;
                }
            }

            try {
                File.Move(backupFilename, launcherFilename);
            } catch (Exception) {
                try {
                    File.Copy(backupFilename, launcherFilename);
                } catch (Exception e) {
                    Logging.Warning("Can’t move restore original file: " + e);
                }
            }

            Logging.Write("Disabled.");
        }

        public bool TryToPrepare() {
            if (IsPatched()) return true;
            Enable();
            return IsPatched();
        }

        private static string FlagFilename => Path.Combine(AcRootDirectory.Instance.RequireValue, "run-game-directly.flag");

        public override void Run() {
            if (!IsPatched()) {
                throw new Exception("Addon isn’t activated properly");
            }

            File.WriteAllText(FlagFilename, AcsName);
            Logging.Warning("Run(), FlagFilename: " + FlagFilename);

            LauncherProcess = Process.Start(new ProcessStartInfo {
                FileName = LauncherFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
        }

        public override void CleanUp() {
            base.CleanUp();

            if (File.Exists(FlagFilename)) {
                Logging.Warning("Flag wasn’t deleted!");
                File.Delete(FlagFilename);
            }
        }
    }
}