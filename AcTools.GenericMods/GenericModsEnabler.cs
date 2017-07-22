using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.GenericMods.Annotations;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;

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
        public static readonly string ConfigFileName = "JSGME.ini";

        private readonly FileSystemWatcher _watcher;
        private readonly bool _useHardLinks;

        public readonly string RootDirectory;
        public readonly string ModsDirectory;

        public ChangeableObservableCollection<GenericMod> Mods { get; }

        public GenericModsEnabler(string rootDirectory, string modsDirectory, bool useHardLinks = true) {
            FileUtils.EnsureDirectoryExists(rootDirectory);
            FileUtils.EnsureDirectoryExists(modsDirectory);

            RootDirectory = rootDirectory;
            ModsDirectory = modsDirectory;
            _useHardLinks = useHardLinks;
            Mods = new ChangeableObservableCollection<GenericMod>();
            ScanMods();

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

        private void OnWatcher(object sender, EventArgs args) {
            _busy.DoDelay(ScanMods, 300);
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

        private void ScanMods() {
            _operationBusy.Do(() => {
                Mods.ReplaceEverythingBy_Direct(Directory.GetDirectories(ModsDirectory)
                                                         .Where(x => Path.GetFileName(x)?.StartsWith("!") == false)
                                                         .Select(x => new GenericMod(this, x)));
                LoadState();
            });
        }

        private IniFile GetState() {
            return new IniFile(Path.Combine(ModsDirectory, ConfigFileName));
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
            var current = ini["MODS"].OrderBy(x => x.Value.AsInt()).ToList();
            for (var i = 0; i < current.Count; i++) {
                if (current[i].Value.AsInt() == i + 1) continue;
                ini["MODS"].Set(current[i].Key, i + 1);
                changed = true;
            }

            return changed;
        }

        private void SaveApplyOrder(IniFile ini, bool forceSave) {
            if (UpdateApplyOrder(ini) || forceSave) {
                LoadState(ini);
                ini.Save();
            }
        }

        public Task EnableAsync([NotNull] GenericMod mod, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (mod.IsEnabled) {
                throw new InformativeException("Can’t enable mod", "Mod is already enabled.");
            }

            return _busy.Delay(() => Task.Run(() => _operationBusy.Do(() => {
                var iniFile = new IniFile(Path.Combine(ModsDirectory, ConfigFileName));
                iniFile["MODS"].Set(mod.DisplayName, int.MaxValue);
                var dependancies = iniFile["DEPENDANCIES"];
                foreach (var dependant in CheckConflicts(mod).Select(x => x.ModName).Distinct()) {
                    var current = dependancies.GetGenericModDependancies(dependant);
                    if (current?.Contains(mod.DisplayName) == true) continue;
                    dependancies.SetGenericModDependancies(dependant, (current ?? new string[0]).Append(mod.DisplayName));
                }
                SaveApplyOrder(iniFile, true);

                var installationLog = GetInstallationLogFilename(ModsDirectory, mod.DisplayName);
                if (File.Exists(installationLog)) {
                    throw new InformativeException("Can’t enable mod", "Mod is already enabled.");
                }

                using (var writer = new StreamWriter(installationLog, false)) {
                    var files = mod.Files;
                    for (var i = 0; i < files.Length; i++) {
                        var file = files[i];
                        if (cancellation.IsCancellationRequested) return;
                        progress?.Report(Tuple.Create(file.RelativeName, (double?)(0.001 + 0.998 * i / files.Length)));

                        try {
                            if (File.Exists(file.Destination)) {
                                FileUtils.EnsureFileDirectoryExists(file.Backup);
                                File.Move(file.Destination, file.Backup);
                            }

                            if (file.Source != null) {
                                FileUtils.EnsureFileDirectoryExists(file.Destination);
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
                // var dependants = Mods.Where(x => x.DependsOn?.Contains(mod.DisplayName) == true).ToList();
                if (mod.DependsOn?.Length > 0) {
                    throw new InformativeException("Can’t disable mod",
                            $"“{mod.DisplayName}” cannot be disabled as {mod.DependsOn.Select(x => $"“{x}”").JoinToReadableString()} has overwritten files and must be removed first.");
                }

                var iniFile = new IniFile(Path.Combine(ModsDirectory, ConfigFileName));
                iniFile["MODS"].Remove(mod.DisplayName);
                var dependancies = iniFile["DEPENDANCIES"];
                foreach (var dependant in dependancies.Select(x => new {
                    x.Key, Values = x.Value.Split(new[] { '"' }, StringSplitOptions.RemoveEmptyEntries)
                }).Where(x => x.Values.Contains(mod.DisplayName)).ToList()) {
                    dependancies.SetGenericModDependancies(dependant.Key, dependant.Values.ApartFrom(mod.DisplayName));
                }
                SaveApplyOrder(iniFile, true);

                var installationLog = GetInstallationLogFilename(ModsDirectory, mod.DisplayName);
                if (!File.Exists(installationLog)) {
                    throw new InformativeException("Can’t disable mod", "Mod is already disabled.");
                }

                var lines = File.ReadAllLines(installationLog);
                File.Delete(installationLog);

                for (var i = 0; i < lines.Length; i++) {
                    var line = lines[i];
                    if (cancellation.IsCancellationRequested) return;
                    progress?.Report(Tuple.Create(line, (double?)(0.001 + 0.998 * i / lines.Length)));

                    try {
                        var backup = GetBackupFilename(ModsDirectory, mod.DisplayName, line);
                        var destination = Path.Combine(RootDirectory, line);

                        if (File.Exists(destination)) {
                            File.Delete(destination);
                            DeleteIfEmpty(Path.GetDirectoryName(destination));
                        }

                        if (File.Exists(backup)) {
                            File.Move(backup, destination);
                            DeleteIfEmpty(Path.GetDirectoryName(backup));
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
            ScanMods();
        }

        public void RenameMod(GenericMod mod, string newLocation) {
            if (mod.IsEnabled) throw new InformativeException("Can’t rename mod", "Mod should be disabled first.");
            _busy.Delay(() => Directory.Move(mod.ModDirectory, newLocation), 300, true);
            ScanMods();
        }
    }
}