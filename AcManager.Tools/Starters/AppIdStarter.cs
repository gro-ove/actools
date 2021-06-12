using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Starters {
    public class AppIdStarter : StarterBase {
        private static string AppIdFilename => Path.Combine(AcRootDirectory.Instance.RequireValue, "steam_appid.txt");

        public static void CleanUpForOthers() {
            FileUtils.TryToDelete(AppIdFilename);
        }

        private static void CreateAppIdFile() {
            var appIdFilename = AppIdFilename;
            if (!File.Exists(appIdFilename)) {
                File.WriteAllText(appIdFilename, CommonAcConsts.AppId);
            }
        }

        public override void Run() {
            SteamRunningHelper.EnsureSteamIsRunning(RunSteamIfNeeded, false);
            CreateAppIdFile();
            RaisePreviewRunEvent(AcsFilename);
            GameProcess = Process.Start(new ProcessStartInfo {
                FileName = AcsFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
            if (GameProcess != null && OptionTrackProcess) {
                ChildProcessTracker.AddProcess(GameProcess);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetAchievementsInner() {
            SteamRunningHelper.EnsureSteamIsRunning(SettingsHolder.Drive.RunSteamIfNeeded, false);
            CreateAppIdFile();

            var outputName = "SteamStatisticsReader-out.json";
            var reader = Path.Combine(AcRootDirectory.Instance.RequireValue, "SteamStatisticsReader.exe");
            var readerBackup = FileUtils.EnsureUnique(reader + ".bak");
            try {
                if (File.Exists(reader)) {
                    File.Move(reader, readerBackup);
                }
                File.Copy(MainExecutingFile.Location, reader);

                using (var process = new Process {
                    StartInfo = {
                        FileName = reader,
                        WorkingDirectory = AcRootDirectory.Instance.RequireValue,
                        Arguments = $"--out={outputName}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                }) {
                    process.Start();
                    process.WaitForExit(10000);
                    if (!process.HasExited) {
                        process.Kill();
                    }
                }
                Thread.Sleep(500);
                var outputFilename = Path.Combine(AcRootDirectory.Instance.RequireValue, outputName);
                Logging.Debug("outputFilename=" + outputFilename);
                Logging.Debug("File.Exists(outputFilename)=" + File.Exists(outputFilename));
                if (!File.Exists(outputFilename)) {
                    throw new Exception("Custom Steam statistics reader failed to do its job properly");
                }

                var result = File.ReadAllText(outputFilename);
                FileUtils.TryToDelete(outputFilename);
                return result;
            } finally {
                if (File.Exists(readerBackup)) {
                    File.Delete(reader);
                    File.Move(readerBackup, reader);
                }
            }

            /*var output = new StringBuilder();
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
            return string.IsNullOrEmpty(result) ? "{}" : result;*/
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