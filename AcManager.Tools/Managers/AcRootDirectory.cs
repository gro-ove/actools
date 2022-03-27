﻿using System;
using System.IO;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class AcRootDirectoryEventArgs : EventArgs {
        public readonly string PreviousValue, NewValue;

        internal AcRootDirectoryEventArgs(string previousValue, string newValue) {
            PreviousValue = previousValue;
            NewValue = newValue;
        }
    }

    public class AcRootDirectory {
        public const string Key = "_ac_root";

        public static AcRootDirectory Instance { get; private set; }

        public static AcRootDirectory Initialize(string directory = null) {
            if (Instance != null) throw new Exception(@"Already initialized");
            return Instance = new AcRootDirectory(directory);
        }

        public bool IsFirstRun { get; }

        private AcRootDirectory(string directory) {
            if (!ValuesStorage.Contains(Key)) IsFirstRun = true;

            Value = (directory ?? ValuesStorage.Get<string>(Key))?.Trim();
            if (Value == null || CheckDirectory(Value, true)) return;

            Logging.Warning($"AC root directory “{Value}” is not valid anymore");
            Value = null;
        }

        public AcDirectories CarsDirectories { get; private set; }
        public AcDirectories TracksDirectories { get; private set; }
        public AcDirectories ShowroomsDirectories { get; private set; }
        public AcDirectories WeatherDirectories { get; private set; }
        public AcDirectories PpFiltersDirectories { get; private set; }
        public AcDirectories DriverModelsDirectories { get; private set; }
        public AcDirectories PythonAppsDirectories { get; private set; }
        public AcDirectories LuaAppsDirectories { get; private set; }
        public AcDirectories FontsDirectories { get; private set; }
        public AcDirectories KunosCareerDirectories { get; private set; }

        public IAcDirectories ReplaysDirectories { get; private set; }
        public AcDirectories UserChampionshipsDirectories { get; private set; }

        private void UpdateDirectories() {
            CarsDirectories?.Obsolete();
            TracksDirectories?.Obsolete();
            ShowroomsDirectories?.Obsolete();
            WeatherDirectories?.Obsolete();
            PpFiltersDirectories?.Obsolete();
            DriverModelsDirectories?.Obsolete();
            PythonAppsDirectories?.Obsolete();
            LuaAppsDirectories?.Obsolete();

            CarsDirectories = Value == null ? null : new AcDirectories(AcPaths.GetCarsDirectory(Value));
            TracksDirectories = Value == null ? null : new AcDirectories(AcPaths.GetTracksDirectory(Value));
            ShowroomsDirectories = Value == null ? null : new AcDirectories(AcPaths.GetShowroomsDirectory(Value));
            WeatherDirectories = Value == null ? null : new AcDirectories(AcPaths.GetWeatherDirectory(Value));
            PpFiltersDirectories = Value == null ? null : new AcDirectories(AcPaths.GetPpFiltersDirectory(Value));
            DriverModelsDirectories = Value == null ? null : new AcDirectories(AcPaths.GetDriverModelsDirectory(Value));
            PythonAppsDirectories = Value == null ? null : new AcDirectories(AcPaths.GetPythonAppsDirectory(Value));
            LuaAppsDirectories = Value == null ? null : new AcDirectories(Path.Combine(Value, "apps", "lua"));
            FontsDirectories = Value == null ? null : new AcDirectories(AcPaths.GetFontsDirectory(Value));
            KunosCareerDirectories = Value == null ? null : new AcDirectories(AcPaths.GetKunosCareerDirectory(Value));

            FileUtils.EnsureDirectoryExists(AcPaths.GetReplaysDirectory());
            ReplaysDirectories = ReplaysDirectories ?? new MultiDirectories(AcPaths.GetReplaysDirectory(), null);

            var champsDirectory = Path.Combine(AcPaths.GetDocumentsDirectory(), "champs");
            if (!Directory.Exists(champsDirectory) && File.Exists(champsDirectory)) {
                champsDirectory = Path.Combine(AcPaths.GetDocumentsDirectory(), "champs_cm");
            }
            UserChampionshipsDirectories = UserChampionshipsDirectories ?? new AcDirectories(champsDirectory);

            CarsDirectories?.CreateIfMissing();
            TracksDirectories?.CreateIfMissing();
            ShowroomsDirectories?.CreateIfMissing();
            WeatherDirectories?.CreateIfMissing();
            PpFiltersDirectories?.CreateIfMissing();
            DriverModelsDirectories?.CreateIfMissing();
            PythonAppsDirectories?.CreateIfMissing();
            LuaAppsDirectories?.CreateIfMissing();
            UserChampionshipsDirectories?.CreateIfMissing();
        }

        private string _value;

        [CanBeNull]
        public string Value {
            get => _value;
            set {
                if (_value == value) return;

                var oldValue = _value;
                _value = CheckDirectory(value, true) ? value : null;

                ValuesStorage.Set(Key, _value);
                UpdateDirectories();

                Changed?.Invoke(this, new AcRootDirectoryEventArgs(oldValue, _value));
            }
        }

        [NotNull]
        public string RequireValue {
            get {
                if (_value == null) throw new Exception(ToolsStrings.AcRootDirectory_Required);
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

        public static bool CheckDirectory(string directory, bool verboseMode) {
            return CheckDirectory(directory, verboseMode, out _);
        }

        public static bool CheckDirectory(string directory, bool verboseMode, out string reason) {
            if (directory == null) {
                reason = ToolsStrings.AcRootDirectory_NotDefined;
                return false;
            }

            if (!AcPaths.OptionEaseAcRootCheck) {
                // We don’t use AcPaths.IsAcRoot() here to get a nice message telling what’s wrong, but logic is similar

                if (!Directory.Exists(directory)) {
                    reason = ToolsStrings.AcRootDirectory_Missing;
                    return false;
                }

                var appsDirectory = Path.Combine(directory, @"apps");
                if (!Directory.Exists(appsDirectory)) {
                    if (verboseMode) {
                        Logging.Warning("Apps folder not found: " + appsDirectory);
                        Logging.Warning("All directories found: " + Directory.GetDirectories(directory).JoinToString(@", "));
                        Logging.Warning("All files found: " + Directory.GetFiles(directory).JoinToString(@", "));
                    }
                    reason = File.Exists(Path.Combine(directory, @"acc.exe"))
                            ? "AC Competizione is not supported, original Assetto Corsa only"
                            : string.Format(ToolsStrings.AcRootDirectory_MissingDirectory, @"apps");
                    return false;
                }

                if (!Directory.Exists(Path.Combine(directory, @"content"))) {
                    reason = string.Format(ToolsStrings.AcRootDirectory_MissingDirectory, @"content");
                    return false;
                }

                if (!Directory.Exists(Path.Combine(directory, @"content", @"cars"))) {
                    reason = string.Format(ToolsStrings.AcRootDirectory_MissingDirectory, @"content\cars");
                    return false;
                }

                if (!File.Exists(Path.Combine(directory, @"acs.exe")) && !File.Exists(Path.Combine(directory, @"acs_pro.exe"))) {
                    reason = string.Format(ToolsStrings.AcRootDirectory_MissingFile, @"acs.exe");
                    return false;
                }
            }

            var launcher = Path.Combine(directory, "AssettoCorsa.exe");
            if (verboseMode) {
                if (!File.Exists(launcher)) {
                    var backup = launcher.ApartFromLast(@".exe", StringComparison.OrdinalIgnoreCase) + @"_backup_ts.exe";
                    if (File.Exists(backup)) {
                        TryToFix(backup, launcher);
                    }
                }

                if (!File.Exists(launcher)) {
                    var backup = launcher.ApartFromLast(@".exe", StringComparison.OrdinalIgnoreCase) + @"_backup_sp.exe";
                    if (File.Exists(backup)) {
                        TryToFix(backup, launcher);
                    }
                }

                /*if (!File.Exists(launcher)) {
                    reason = string.Format(ToolsStrings.AcRootDirectory_MissingFile, @"AssettoCorsa.exe");
                    return false;
                }*/
            }

            reason = null;
            return true;
        }

        public static string TryToFind() {
            return AcRootFinder.TryToFind();
        }
    }
}