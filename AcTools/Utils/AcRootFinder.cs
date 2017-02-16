using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AcTools.Utils {
    public static class AcRootFinder {
        private static IEnumerable<string> GetSteamAppsDirectories() {
            var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (regKey == null) yield break;

            yield return Path.GetDirectoryName(regKey.GetValue("SourceModInstallPath").ToString());

            var steamPath = regKey.GetValue("SteamPath").ToString();
            var config = File.ReadAllText(Path.Combine(steamPath, @"config", @"config.vdf"));

            var match = Regex.Match(config, "\"BaseInstallFolder_\\d\"\\s+\"(.+?)\"");
            while (match.Success) {
                if (match.Groups.Count > 1) {
                    yield return Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), "SteamApps");
                }

                match = match.NextMatch();
            }
        }

        public static string TryToFind() {
            try {
                var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                return regKey == null ? null :
                        (from searchCandidate in GetSteamAppsDirectories()
                         where searchCandidate != null && Directory.Exists(searchCandidate)
                         select Path.Combine(searchCandidate, @"common", @"assettocorsa")).FirstOrDefault(Directory.Exists);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }
    }
}
