using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Tools.Managers {
    public class AcRootDirectoryEventArgs : EventArgs {
        public readonly string PreviousValue, NewValue;

        internal AcRootDirectoryEventArgs(string previousValue, string newValue) {
            PreviousValue = previousValue;
            NewValue = newValue;
        }
    }

    public class AcRootDirectory {
        public static bool OptionDisableChecking = false;

        public const string Key = "_ac_root";

        public static AcRootDirectory Instance { get; private set; }

        public static AcRootDirectory Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new AcRootDirectory();
        }

        private AcRootDirectory() {
            Value = ValuesStorage.GetString(Key);
            if (Value == null || CheckDirectory(Value)) return;

            Logging.Warning("ac root directory '{0}' is not valid anymore", Value);
            Value = null;
        }

        public AcObjectTypeDirectories CarsDirectories { get; private set; }
        public AcObjectTypeDirectories TracksDirectories { get; private set; }
        public AcObjectTypeDirectories ShowroomsDirectories { get; private set; }
        public AcObjectTypeDirectories WeatherDirectories { get; private set; }
        public AcObjectTypeDirectories PpFiltersDirectories { get; private set; }
        public AcObjectTypeDirectories ReplaysDirectories { get; private set; }
        public AcObjectTypeDirectories KunosCareerDirectories { get; private set; }

        private void UpdateDirectories() {
            CarsDirectories?.Obsolete();
            TracksDirectories?.Obsolete();
            ShowroomsDirectories?.Obsolete();
            WeatherDirectories?.Obsolete();
            PpFiltersDirectories?.Obsolete();

            CarsDirectories = Value == null ? null : new AcObjectTypeDirectories(FileUtils.GetCarsDirectory(Value));
            TracksDirectories = Value == null ? null : new AcObjectTypeDirectories(FileUtils.GetTracksDirectory(Value));
            ShowroomsDirectories = Value == null ? null : new AcObjectTypeDirectories(FileUtils.GetShowroomsDirectory(Value));
            WeatherDirectories = Value == null ? null : new AcObjectTypeDirectories(FileUtils.GetWeatherDirectory(Value));
            PpFiltersDirectories = Value == null ? null : new AcObjectTypeDirectories(FileUtils.GetPpFiltersDirectory(Value));
            KunosCareerDirectories = Value == null ? null : new AcObjectTypeDirectories(FileUtils.GetKunosCareerDirectory(Value));

            ReplaysDirectories = ReplaysDirectories ?? new AcObjectTypeDirectories(FileUtils.GetReplaysDirectory(), null);

            CarsDirectories?.CreateIfMissing();
            TracksDirectories?.CreateIfMissing();
            ShowroomsDirectories?.CreateIfMissing();
            WeatherDirectories?.CreateIfMissing();
            PpFiltersDirectories?.CreateIfMissing();
            ReplaysDirectories?.CreateIfMissing();
        }

        private string _value;

        [CanBeNull]
        public string Value {
            get {
                return _value;
            }
            set {
                if (_value == value) return;

                var oldValue = _value;
                _value = CheckDirectory(value) ? value : null;

                ValuesStorage.Set(Key, _value);
                UpdateDirectories();
                
                Changed?.Invoke(this, new AcRootDirectoryEventArgs(oldValue, _value));
            }
        }

        [NotNull]
        public string RequireValue {
            get {
                if (_value == null) throw new Exception("AcRootDirectory is required");
                return _value;
            }
        }
        
        public void Reset() {
            ValuesStorage.Remove(Key);
        }

        public bool IsReady => _value != null;

        public delegate void AcRootDirectoryEventHandler(object sender, AcRootDirectoryEventArgs e);
        public event AcRootDirectoryEventHandler Changed;

        private static void TryToFix(string from, string to) {
            try {
                File.Move(from, to);
                return;
            } catch (Exception) {
                // ignored
            }

            try {
                File.Copy(from, to);
                File.Delete(from);
            } catch (Exception) {
                // ignored
            }
        }

        public static bool CheckDirectory(string directory) {
            if (directory == null || (!OptionDisableChecking &&
                    (!Directory.Exists(directory) || !Directory.Exists(Path.Combine(directory, "apps", "python")) ||
                            !Directory.Exists(Path.Combine(directory, "content", "cars")) ||
                            !Directory.Exists(Path.Combine(directory, "content", "tracks")) || !File.Exists(Path.Combine(directory, "acs.exe"))))) {
                return false;
            }

            var launcher = Path.Combine(directory, "AssettoCorsa.exe");
            if (!File.Exists(launcher)) {
                var backup = launcher.ApartFromLast(".exe") + "_backup_ts.exe";
                if (File.Exists(backup)) {
                    TryToFix(backup, launcher);
                }
            }

            if (!File.Exists(launcher)) {
                var backup = launcher.ApartFromLast(".exe") + "_backup_sp.exe";
                if (File.Exists(backup)) {
                    TryToFix(backup, launcher);
                }
            }

            return File.Exists(launcher);
        }

        public static string TryToFind() {
            Logging.Write("trying to find ac dir from steam");
            try {
                var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (regKey == null) return null;

                var searchCandidates = new List<string>();

                var installPath = Path.GetDirectoryName(regKey.GetValue("SourceModInstallPath").ToString());
                searchCandidates.Add(installPath);
                Logging.Write("- search candidate: {0}", installPath);

                var steamPath = regKey.GetValue("SteamPath").ToString();
                var config = File.ReadAllText(Path.Combine(steamPath, "config", "config.vdf"));

                var match = Regex.Match(config, "\"BaseInstallFolder_\\d\"\\s+\"(.+?)\"");
                while (match.Success) {
                    if (match.Groups.Count > 1) {
                        var candidate = Path.Combine(match.Groups[1].Value.Replace(@"\\", @"\"), "SteamApps");
                        searchCandidates.Add(candidate);
                        Logging.Write("- search candidate: {0}", candidate);
                    }
                    match = match.NextMatch();
                }

                var result = (
                    from searchCandidate in searchCandidates
                    where searchCandidate != null && Directory.Exists(searchCandidate)
                    select Path.Combine(searchCandidate, "common", "assettocorsa")
                ).FirstOrDefault(Directory.Exists);
                Logging.Write("- result: {0}", result);
                return result;
            } catch (SecurityException exception) {
                Logging.Write("- error: {0}", exception);
                return null;
            }
        }
    }
}
