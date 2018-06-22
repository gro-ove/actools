using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcTools.GenericMods {
    public static class GenericModsExtension {
        [CanBeNull]
        public static string[] GetGenericModDependancies(this IniFileSection section, string key) {
            return section.GetNonEmpty(key)?.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static void SetGenericModDependancies(this IniFileSection section, string key, [CanBeNull] IEnumerable<string> values) {
            var v = values?.OrderBy(x => x).Select(x => $"\"{x}\"").JoinToString();
            if (string.IsNullOrWhiteSpace(v)) {
                section.Remove(key);
            } else {
                section.Set(key, v);
            }
        }
    }

    public class GenericModsEnabler : IDisposable {
        public static bool OptionLoggingEnabled = false;
        public static readonly string ConfigFileName = "JSGME.ini";

        private readonly FileSystemWatcher _watcher;
        private readonly bool _useHardLinks;

        public readonly string RootDirectory;
        public readonly string ModsDirectory;
        public readonly string ConfigFilename;

        public ChangeableObservableCollection<GenericMod> Mods { get; }

        private static GenericModsEnabler _instance;
        private static int _instanceParameters;
        private static readonly TaskCache InstanceTaskCache = new TaskCache();

        private static int GetParameters(string rootDirectory, string modsDirectory, bool useHardLinks) {
            return (((rootDirectory.GetHashCode() * 397) ^ modsDirectory.GetHashCode()) * 397) ^ useHardLinks.GetHashCode();
        }

        [ItemCanBeNull]
        private static Task<GenericModsEnabler> GetInstanceAsyncInner(string rootDirectory, string modsDirectory, bool useHardLinks) {
            FileUtils.EnsureDirectoryExists(modsDirectory);
            return Task.Run(() => {
                _instanceParameters = GetParameters(rootDirectory, modsDirectory, useHardLinks);
                _instance = new GenericModsEnabler(rootDirectory,
                        modsDirectory, useHardLinks);
                return _instance;
            });
        }

        public static Task<GenericModsEnabler> GetInstanceAsync(string rootDirectory, string modsDirectory, bool useHardLinks = true) {
            if (_instance != null) {
                if (GetParameters(rootDirectory, modsDirectory, useHardLinks) == _instanceParameters) {
                    return Task.FromResult(_instance);
                }

                DisposeHelper.Dispose(ref _instance);
            }
            return InstanceTaskCache.Get(() => GetInstanceAsyncInner(rootDirectory, modsDirectory, useHardLinks));
        }

        private GenericModsEnabler(string rootDirectory, string modsDirectory, bool useHardLinks = true) {
            FileUtils.EnsureDirectoryExists(rootDirectory);
            FileUtils.EnsureDirectoryExists(modsDirectory);

            RootDirectory = rootDirectory;
            ModsDirectory = modsDirectory;
            ConfigFilename = Path.Combine(ModsDirectory, ConfigFileName);

            _useHardLinks = useHardLinks;
            Mods = new ChangeableObservableCollection<GenericMod>();
            ScanMods(false);

            FileUtils.EnsureDirectoryExists(modsDirectory);
            _watcher = new FileSystemWatcher {
                Path = modsDirectory,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*",
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            _watcher.Changed += OnWatcher;
            _watcher.Created += OnWatcher;
            _watcher.Deleted += OnWatcher;
            _watcher.Renamed += OnWatcher;
        }

        private readonly Busy _busy = new Busy(true);
        private readonly Busy _operationBusy = new Busy(true);
        private readonly List<string> _changedFilesToRescan = new List<string>();

        private void OnWatcher(object sender, FileSystemEventArgs args) {
            if (_busy.Is) {
                _changedFilesToRescan.Add(args.FullPath);
            } else {
                _changedFilesToRescan.Clear();
                _changedFilesToRescan.Add(args.FullPath);
                _busy.DoDelay(() => {
                    if (_changedFilesToRescan.Count == 0) return;
                    ScanMods(false, _changedFilesToRescan.ToArray());
                }, 300);
            }
        }

        private void OnWatcher(object sender, RenamedEventArgs args) {
            if (_busy.Is) {
                _changedFilesToRescan.Add(args.OldFullPath);
                _changedFilesToRescan.Add(args.FullPath);
            } else {
                _changedFilesToRescan.Clear();
                _changedFilesToRescan.Add(args.OldFullPath);
                _changedFilesToRescan.Add(args.FullPath);
                _busy.DoDelay(() => {
                    if (_changedFilesToRescan.Count == 0) return;
                    ScanMods(false, _changedFilesToRescan.ToArray());
                }, 300);
            }
        }

        public void ReloadList() {
            _changedFilesToRescan.Clear();
            ScanMods(true);
        }

        public static string GetBackupFilename(string modsDirectory, string modName, string relative) {
            return Path.Combine(modsDirectory, "!BACKUP", $"{relative}.{modName}");
        }

        public static string GetInstallationLogFilename(string modsDirectory, string modName) {
            var result = Path.Combine(modsDirectory, "!INSTLOGS", $"{modName} install.log");
            FileUtils.EnsureFileDirectoryExists(result);
            return result;
        }

        [CanBeNull]
        public GenericMod GetByName([CanBeNull] string name) {
            return Mods.FirstOrDefault(x => x.DisplayName == name);
        }

        private DateTime? _lastSaved;

        private void ScanMods(bool force, params string[] filenames) {
            void Scan() {
                var directories = Directory.GetDirectories(ModsDirectory).Where(x => Path.GetFileName(x)?.StartsWith("!") == false).ToList();
                var replaceMods = force || !directories.SequenceEqual(Mods.Select(x => x.ModDirectory));
                if (replaceMods) {
                    Mods.ReplaceEverythingBy_Direct(Directory.GetDirectories(ModsDirectory).Where(x => Path.GetFileName(x)?.StartsWith("!") == false).Select(x => new GenericMod(this, x)));
                } else {
                    foreach (var changed in filenames.Where(x => x?.EndsWith(GenericMod.DescriptionExtension, StringComparison.OrdinalIgnoreCase) == true)) {
                        Mods.FirstOrDefault(x => FileUtils.IsAffectedBy(changed, x.ModDirectory))?.Description.Reset();
                    }
                }

                if (replaceMods || filenames.Any(x => x == null || FileUtils.ArePathsEqual(x, ConfigFilename))) {
                    var state = new FileInfo(ConfigFileName);
                    if (!_lastSaved.HasValue || !state.Exists || state.LastWriteTime > _lastSaved) {
                        LoadState();
                    }
                }
            }

            _operationBusy.Do(Scan);
        }

        private IniFile GetState() {
            return new IniFile(ConfigFilename, IniFileMode.SquareBracketsWithin);
        }

        private void LoadState(IniFile state) {
            var modsSection = state["MODS"];
            foreach (var mod in Mods) {
                mod.AppliedOrder = modsSection.GetInt(mod.DisplayName, -1);
            }

            var dependanciesSection = state["DEPENDANCIES"];
            foreach (var mod in Mods) {
                mod.DependsOn = dependanciesSection.GetNonEmpty(mod.DisplayName)?.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private void LoadState() {
            LoadState(GetState());
        }

        public Task<GenericModFile[]> CheckConflictsAsync([NotNull] GenericMod mod) {
            return Task.Run(() => CheckConflicts(mod).ToArray());
        }

        private IEnumerable<GenericModFile> CheckConflicts([NotNull] GenericMod mod) {
            if (mod.IsEnabled) yield break;

            var modFiles = mod.Files;
            foreach (var enabled in Mods.Where(x => x.IsEnabled)) {
                var enabledFiles = enabled.Files;
                foreach (var file in modFiles) {
                    var conflict = enabledFiles.FirstOrDefault(x => x.Destination == file.Destination);
                    if (conflict != null) {
                        yield return conflict;
                    }
                }
            }
        }

        public static bool UpdateApplyOrder(IniFile ini) {
            var changed = false;
            var current = ini["MODS"].OrderBy(x => x.Value.As<int>()).ToList();
            for (var i = 0; i < current.Count; i++) {
                if (current[i].Value.As<int>() == i + 1) continue;
                ini["MODS"].Set(current[i].Key, i + 1);
                changed = true;
            }

            return changed;
        }

        private void SaveApplyOrder(IniFile ini, bool forceSave) {
            if (UpdateApplyOrder(ini) || forceSave) {
                LoadState(ini);
                ini.Save();
                _lastSaved = DateTime.Now;
            }
        }

        public Task EnableAsync([NotNull] GenericMod mod, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default) {
            if (mod.IsEnabled) {
                throw new InformativeException("Can’t enable mod", "Mod is already enabled.");
            }

            return _busy.Delay(() => Task.Run(() => _operationBusy.Do(() => {
                Debug($"Enabling {mod.DisplayName}…");

                var iniFile = new IniFile(ConfigFilename, IniFileMode.SquareBracketsWithin);
                iniFile["MODS"].Set(mod.DisplayName, int.MaxValue);
                var dependancies = iniFile["DEPENDANCIES"];
                foreach (var dependant in CheckConflicts(mod).Select(x => x.ModName).Distinct()) {
                    var current = dependancies.GetGenericModDependancies(dependant);
                    if (current?.ArrayContains(mod.DisplayName) == true) continue;
                    dependancies.SetGenericModDependancies(dependant, (current ?? new string[0]).Append(mod.DisplayName));
                }
                SaveApplyOrder(iniFile, true);

                var installationLog = GetInstallationLogFilename(ModsDirectory, mod.DisplayName);
                Debug($"Installation log: {installationLog}");

                if (File.Exists(installationLog)) {
                    throw new InformativeException("Can’t enable mod", "Mod is already enabled.");
                }

                using (var writer = new StreamWriter(installationLog, false)) {
                    var files = mod.Files;
                    for (var i = 0; i < files.Length; i++) {
                        var file = files[i];

                        if (file.RelativeName.EndsWith(".jsgme", StringComparison.OrdinalIgnoreCase)) {
                            Debug($"File, src={file.Source}, ignore as description");
                            continue;
                        }

                        Debug($"File, src={file.Source}, dst={file.Destination})");

                        if (cancellation.IsCancellationRequested) return;
                        progress?.Report(Tuple.Create(file.RelativeName, (double?)(0.001 + 0.998 * i / files.Length)));

                        try {
                            if (File.Exists(file.Destination)) {
                                Debug($"Already exists, moving to {file.Backup}");
                                FileUtils.EnsureFileDirectoryExists(file.Backup);
                                File.Move(file.Destination, file.Backup);
                            }

                            if (file.Source != null) {
                                FileUtils.EnsureFileDirectoryExists(file.Destination);
                                Debug($"Copying to {file.Destination}");

                                if (_useHardLinks) {
                                    FileUtils.HardLinkOrCopy(file.Source, file.Destination, true);
                                } else {
                                    File.Copy(file.Source, file.Destination, true);
                                }
                            }

                            writer.WriteLine(file.RelativeName);
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }
                }
            })), 300, true);
        }

        private void DeleteIfEmpty(string directory) {
            while (directory != null && !FileUtils.ArePathsEqual(directory, ModsDirectory) && !FileUtils.ArePathsEqual(directory, RootDirectory) &&
                    Directory.Exists(directory) && FileUtils.IsDirectoryEmpty(directory)) {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }

        public Task DisableAsync([NotNull] GenericMod mod, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (!mod.IsEnabled) {
                throw new InformativeException("Can’t disable mod", "Mod is already disabled.");
            }

            return _busy.Delay(() => Task.Run(() => _operationBusy.Do(() => {
                Debug($"Disabling {mod.DisplayName}…");

                // var dependants = Mods.Where(x => x.DependsOn?.Contains(mod.DisplayName) == true).ToList();
                if (mod.DependsOn?.Length > 0) {
                    throw new InformativeException("Can’t disable mod",
                            $"“{mod.DisplayName}” cannot be disabled as {mod.DependsOn.Select(x => $"“{x}”").JoinToReadableString()} has overwritten files and must be removed first.");
                }

                var iniFile = new IniFile(ConfigFilename, IniFileMode.SquareBracketsWithin);
                iniFile["MODS"].Remove(mod.DisplayName);
                var dependancies = iniFile["DEPENDANCIES"];
                foreach (var dependant in dependancies.Select(x => new {
                    x.Key,
                    Values = x.Value.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries)
                }).Where(x => x.Values.ArrayContains(mod.DisplayName)).ToList()) {
                    dependancies.SetGenericModDependancies(dependant.Key, dependant.Values.ApartFrom(mod.DisplayName));
                }
                SaveApplyOrder(iniFile, true);

                var installationLog = GetInstallationLogFilename(ModsDirectory, mod.DisplayName);
                Debug($"Installation log: {installationLog}");

                if (!File.Exists(installationLog)) {
                    throw new InformativeException("Can’t disable mod", "Mod is already disabled.");
                }

                var lines = File.ReadAllLines(installationLog);
                Debug($"Lines in log: {lines.Length}");
                File.Delete(installationLog);

                for (var i = 0; i < lines.Length; i++) {
                    var line = lines[i];
                    Debug($"Line #{i + 1}: {line}");

                    if (cancellation.IsCancellationRequested) return;
                    progress?.Report(Tuple.Create(line, (double?)(0.001 + 0.998 * i / lines.Length)));

                    try {
                        var backup = GetBackupFilename(ModsDirectory, mod.DisplayName, line);
                        var destination = Path.Combine(RootDirectory, line);

                        Debug($"Backup: {backup}");
                        Debug($"Destination: {destination}");

                        if (File.Exists(destination)) {
                            Debug("Removing existing destination…");
                            File.Delete(destination);
                        }

                        if (File.Exists(backup)) {
                            Debug("Restoring existing backup…");
                            FileUtils.EnsureFileDirectoryExists(destination);
                            File.Move(backup, destination);
                            DeleteIfEmpty(Path.GetDirectoryName(backup));
                        } else {
                            DeleteIfEmpty(Path.GetDirectoryName(destination));
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }
            })), 300, true);
        }

        public void Dispose() {
            _watcher.Dispose();
        }

        public void DeleteMod(GenericMod mod) {
            if (mod.IsEnabled) throw new InformativeException("Can’t delete mod", "Mod should be disabled first.");
            _busy.Delay(() => FileUtils.Recycle(mod.ModDirectory), 300, true);
            ScanMods(true);
        }

        public void RenameMod(GenericMod mod, string newLocation) {
            if (mod.IsEnabled) throw new InformativeException("Can’t rename mod", "Mod should be disabled first.");
            _busy.Delay(() => Directory.Move(mod.ModDirectory, newLocation), 300, true);
            ScanMods(true);
        }

        private static void Debug(string message, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            if (OptionLoggingEnabled) {
                Logging.Debug(message, m, p, l);
            }
        }
    }
}