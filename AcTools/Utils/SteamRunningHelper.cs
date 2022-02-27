using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AcTools.Utils.Helpers;
using Microsoft.Win32;

namespace AcTools.Utils {
    public static class SteamRunningHelper {
        private static string GetSteamDirectory() {
            var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var ret = regKey?.GetValue("SteamPath").ToString();
            regKey?.Close();
            return ret;
        }

        private static void TryToRunSteam(string steamDirectory, bool launchAc) {
            try {
                Process.Start(Path.Combine(steamDirectory, "Steam.exe"), launchAc ?
                        $"-silent -applaunch {CommonAcConsts.AppId.ToInvariantString()}" : "-silent");
                Thread.Sleep(2000);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            }
        }

        public static void EnsureSteamIsRunning(bool tryToRun, bool launchAc) {
            if (Process.GetProcessesByName("steam").Length > 0) return;

            var steamDirectory = GetSteamDirectory();
            if (steamDirectory == null) return;

            if (tryToRun) {
                TryToRunSteam(steamDirectory, launchAc);
                if (Process.GetProcessesByName("steam").Length == 0) {
                    throw new Exception("Couldn’t run Steam");
                }
            }

            // throw new Exception("Running Steam is required");
        }
    }
}