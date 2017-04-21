using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace AcTools.Utils {
    public static class SteamRunningHelper {
        private static string GetSteamDirectory() {
            var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return regKey?.GetValue("SteamPath").ToString();
        }

        private static void TryToRunSteam() {
            try {
                Process.Start(Path.Combine(GetSteamDirectory(), "Steam.exe"), "-silent");
                Thread.Sleep(2000);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            }
        }

        public static void EnsureSteamIsRunning(bool tryToRun) {
            if (Process.GetProcessesByName("steam").Length > 0) return;

            if (tryToRun) {
                TryToRunSteam();
                if (Process.GetProcessesByName("steam").Length == 0) {
                    throw new Exception("Couldn’t run Steam");
                }
            }

            // throw new Exception("Running Steam is required");
        }
    }
}