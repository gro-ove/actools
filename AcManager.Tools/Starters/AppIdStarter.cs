using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Starters {
    public class AppIdStarter : StarterBase {
        private static string AppIdFilename => Path.Combine(AcRootDirectory.Instance.RequireValue, "steam_appid.txt");

        private static void CreateAppIdFile() {
            var appIdFilename = AppIdFilename;
            if (!File.Exists(appIdFilename)) {
                File.WriteAllText(appIdFilename, CommonAcConsts.AppId);
            }
        }

        public override void Run() {
            SteamRunningHelper.EnsureSteamIsRunning(RunSteamIfNeeded, false);
            CreateAppIdFile();
            GameProcess = Process.Start(new ProcessStartInfo {
                FileName = AcsFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetAchievementsInner() {
            SteamRunningHelper.EnsureSteamIsRunning(SettingsHolder.Drive.RunSteamIfNeeded, false);
            CreateAppIdFile();

            var reader = Path.Combine(AcRootDirectory.Instance.RequireValue, "SteamStatisticsReader.exe");
            var output = new StringBuilder();
            using (var process = new Process {
                StartInfo = {
                    FileName = reader,
                    WorkingDirectory = AcRootDirectory.Instance.RequireValue,
                    Arguments = "-c",
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            }) {
                process.Start();
                process.OutputDataReceived += (sender, args) => {
                    if (args.Data != null) output.Append(args.Data);
                };
                process.BeginOutputReadLine();
                process.WaitForExit(10000);
                if (!process.HasExited) {
                    process.Kill();
                }
            }

            var result = Regex.Replace(output.ToString(), @"\r+|\s+|\n$", "");
            return string.IsNullOrEmpty(result) ? "{}" : result;
        }

        [CanBeNull]
        public static string GetAchievements() {
            try {
                return GetAchievementsInner();
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }
    }
}