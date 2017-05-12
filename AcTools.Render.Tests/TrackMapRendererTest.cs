using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.Render.Kn5SpecificSpecial;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace AcTools.Render.Tests {
    internal static class AcRootFinder {
        private static string _value;
        private static bool _found;

        public static string Find() {
            if (!_found) {
                _found = true;

                try {
                    var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                    if (regKey == null) return null;

                    var searchCandidates = new List<string>();

                    var installPath = Path.GetDirectoryName(regKey.GetValue("SourceModInstallPath").ToString());
                    searchCandidates.Add(installPath);

                    var steamPath = regKey.GetValue("SteamPath").ToString();
                    var config = File.ReadAllText(Path.Combine(steamPath, @"config", @"config.vdf"));

                    var match = Regex.Match(config, "\"BaseInstallFolder_\\d\"\\s+\"(.+?)\"");
                    while (match.Success) {
                        if (match.Groups.Count > 1) {
                            var candidate = Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), "SteamApps");
                            searchCandidates.Add(candidate);
                        }
                        match = match.NextMatch();
                    }

                    _value = (from searchCandidate in searchCandidates
                              where searchCandidate != null && Directory.Exists(searchCandidate)
                              select Path.Combine(searchCandidate, @"common", @"assettocorsa")).FirstOrDefault(Directory.Exists);
                } catch (Exception) {
                    // ignored
                }
            }

            return _value;
        }
    }

    [TestClass]
    public class TrackMapRendererTest {
        [TestMethod]
        public void HitabashiPark() {
            var path = Path.Combine(AcRootFinder.Find(), @"content\tracks\hitabashipark\hitabashipark.kn5");
            var output = Path.Combine(Path.GetDirectoryName(path) ?? "", "map.png");
            var ini = Path.Combine(Path.GetDirectoryName(path) ?? "", "data", "map.ini");
            if (!File.Exists(path)) {
                Debug.WriteLine("REQUIRED ASSET IS MISSING, TEST CANNOT BE DONE");
                return;
            }

            using (var renderer = new TrackMapRenderer(path) { Scale = 3.4f, Margin = 50f }) {
                renderer.Shot(output);
                renderer.SaveInformation(ini);
            }
        }
    }
}
