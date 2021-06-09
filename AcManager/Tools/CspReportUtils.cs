using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools {
    public static class CspReportUtils {
        public static string OptionLocation;

        private class SummaryGenerator {
            private readonly StringBuilder _result = new StringBuilder();
            private readonly string _log;
            private readonly string _acErrors;
            private readonly string _pyLog;
            private readonly string _cspLog;
            private readonly IniFile _controls;
            private readonly IniFile _race;
            private readonly IniFile _video;
            private readonly IniFile _python;

            public SummaryGenerator(ZipArchive archive) {
                _log = archive.ReadString(@"log.txt");
                _acErrors = archive.ReadString(@"errors.txt");
                _pyLog = archive.ReadString(@"py_log.txt");
                _cspLog = archive.ReadString(@"custom_shaders_patch.log");
                _controls = IniFile.Parse(archive.ReadString(@"controls.ini"));
                _race = IniFile.Parse(archive.ReadString(@"race.ini"));
                _video = IniFile.Parse(archive.ReadString(@"video.ini"));
                _python = IniFile.Parse(archive.ReadString(@"python.ini"));
            }

            public string Run() {
                Collect();
                return _result.ToString();
            }

            private void Group(string title) {
                _result.Append(_result.Length > 0 ? "\n  [ " : "  [ ");
                _result.Append((" ".RepeatString((16 - title.Length) / 2) + title).PadRight(16));
                _result.Append(" ]\n");
            }

            private void Add(string key, string value) {
                if (string.IsNullOrEmpty(value)) return;
                _result.Append((key + ": ").PadRight(12));
                _result.Append(value.Replace("\n", "\n" + " ".RepeatString(12)));
                _result.Append("\n");
            }

            private void Add(string key, bool value) {
                Add(key, value ? "Yes" : "No");
            }

            private string Match([RegexPattern] string regex) {
                return Regex.Match(_log, regex).Groups[1].Value;
            }

            private bool Contains([RegexPattern] string regex) {
                return Regex.IsMatch(_log, regex);
            }

            private void Collect() {
                Group("Versions");
                Add("AC", Match(@"AC VERSION : (.*)"));
                Add("CSP", Match(@"Custom Shaders Patch, (\S*)").TrimEnd('.'));

                Group("Hardware");
                Add("Cores", Match(@"LOGICAL CPU DETECTED: (.*)"));
                Add("GPU", Match(@"ADAPTER DESCRIPTION\s+DESCRIPTION: (.*)"));
                Add("VRAM", Match(@"VIDEO MEM: (.*)"));

                Group("Configuration");
                Add("Mode", _video["CAMERA"].GetNonEmpty("MODE"));
                Add("Resolution", $"{_video["VIDEO"].GetInt("WIDTH", 0)}×{_video["VIDEO"].GetInt("HEIGHT", 0)}");
                Add("Fullscreen", _video["VIDEO"].GetBool("FULLSCREEN", false));
                Add("YEBIS", _video["POST_PROCESS"].GetBool("ENABLED", false));
                Add("FXAA", _video["EFFECTS"].GetBool("FXAA", false));
                Add("AC blur", _video["EFFECTS"].GetBool("MOTION_BLUR", false));
                Add("Apps", _python.Keys.Where(x => _python[x].GetBool("ACTIVE", false)).OrderBy(x => x)
                        .JoinToString(", ").ToLowerInvariant());
                Add("Input", _controls["HEADER"].GetNonEmpty("INPUT_METHOD"));
                Add("Devices", _controls["CONTROLLERS"].Where(x => x.Key.StartsWith("CON")).Select(x => x.Value).JoinToString(", "));

                Group("Race");
                Add("Type", _race["REMOTE"].GetBool("ACTIVE", false) ? "Online"
                        : _race["REPLAY"].GetBool("ACTIVE", false) ? "Replay" : "Offline");
                Add("Track", _race["RACE"].GetNonEmpty("TRACK"));
                Add("Layout", _race["RACE"].GetNonEmpty("CONFIG_TRACK"));
                Add("Car", _race["RACE"].GetNonEmpty("MODEL"));

                Group("Errors");
                Add("Clean", Contains("CLEAN EXIT"));
                Add("Online", Match("ACClient:: (.+)"));
                Add("Race", Match("ERROR: RaceManager :: (.+)"));
                Add("CSP", _cspLog.Split('\n').Where(x => x.Contains("| ERROR |"))
                        .Select(x => x.Split(new[] { '|' }, 3).ElementAtOrDefault(2)?.TrimStart()).NonNull().JoinToString("\n"));
                Add("Stack", _log.Split(new[] { "CRASH in:" }, StringSplitOptions.None).ElementAtOrDefault(1).Or("none").Trim());

                Group("Other logs");
                Add("Python", _pyLog);
                Add("Warnings", _acErrors);
            }
        }

        private static void LaunchReport(string filename) {
            try {
                using (var stream = File.OpenRead(Path.Combine(Path.GetDirectoryName(filename) ?? "", Path.GetFileNameWithoutExtension(filename) + ".zip")))
                using (var archive = new ZipArchive(stream)) {
                    // Apply video config
                    var currentVideo = new IniFile(AcPaths.GetCfgVideoFilename());
                    var currentWidth = currentVideo["VIDEO"].GetInt("WIDTH", 1920);
                    var currentHeight = currentVideo["VIDEO"].GetInt("HEIGHT", 1080);
                    var newVideo =
                            IniFile.Parse((archive.GetEntry("video.ini") ?? throw new Exception("Video config is missing")).Open().ReadAsStringAndDispose());
                    newVideo["VIDEO"].Set("WIDTH", Math.Min(currentWidth, newVideo["VIDEO"].GetInt("WIDTH", 1920)));
                    newVideo["VIDEO"].Set("HEIGHT", Math.Min(currentHeight, newVideo["VIDEO"].GetInt("HEIGHT", 1080)));
                    File.WriteAllText(AcPaths.GetCfgVideoFilename(), newVideo.ToString());

                    // Apply CSP config
                    PatchSettingsModel.Create().ImportFromPresetData(
                            (archive.GetEntry("csp.ini") ?? throw new Exception("CSP config is missing")).Open().ReadAsStringAndDispose());

                    // Apply race config
                    File.WriteAllText(AcPaths.GetRaceIniFilename(),
                            (archive.GetEntry("race.ini") ?? throw new Exception("Race config is missing")).Open().ReadAsStringAndDispose());

                    Process.Start(new ProcessStartInfo {
                        FileName = Path.Combine(AcRootDirectory.Instance.Value ?? "", "acs.exe"),
                        WorkingDirectory = AcRootDirectory.Instance.Value ?? ""
                    });
                }
            } catch (Exception e) {
                NonfatalError.Notify("Failed to launch report race", e);
            }
        }

        private static void UnwrapReport(string filename) {
            var location = OptionLocation;
            if (string.IsNullOrWhiteSpace(location)) {
                location = FilesStorage.Instance.GetTemporaryDirectory("Reports");
            }

            try {
                using (var stream = File.OpenRead(filename))
                using (var archive = new ZipArchive(stream)) {
                    var log = archive.ReadString(@"log.txt");
                    var username = Regex.Match(log, @"Steam Name:(\S*)").Groups[1].Value;
                    username = Regex.Replace(username, "[^A-Za-z0-9-]+", "");

                    if (string.IsNullOrEmpty(username)) {
                        username = IniFile.Parse(archive.ReadString(@"race.ini"))["CAR_0"].GetNonEmpty("DRIVER_NAME") ?? "";
                        username = Regex.Replace(username, "[^A-Za-z0-9-]+", "").Or("-");
                    }

                    var crashId = username + "_" + Path.GetFileNameWithoutExtension(filename).Replace(@"crash_", "");
                    var destination = Path.Combine(location, crashId);
                    if (!Directory.Exists(destination)) {
                        Directory.CreateDirectory(destination);
                    }

                    var dump = archive.Entries.FirstOrDefault(x => x.Name.EndsWith(".dmp"))?.Open().ReadAsBytesAndDispose();
                    if (dump != null) {
                        File.WriteAllBytes(Path.Combine(destination, crashId + ".dmp"), dump);
                    }

                    File.WriteAllText(Path.Combine(destination, crashId + ".txt"), new SummaryGenerator(archive).Run());
                    File.WriteAllText(Path.Combine(destination, crashId + ".report-launch"), "");
                    File.WriteAllBytes(Path.Combine(destination, crashId + ".zip"), File.ReadAllBytes(filename));

                    WindowsHelper.ViewDirectory(destination);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Failed to load report", e);
            }
        }

        public static void Load(string filename) {
            if (filename.EndsWith(@".report-launch", StringComparison.OrdinalIgnoreCase)) {
                LaunchReport(filename);
            } else {
                UnwrapReport(filename);
            }
        }
    }
}