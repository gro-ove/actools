using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public class UserChampionshipsProgress : NotifyPropertyChanged, IDisposable {
        private static UserChampionshipsProgress _instance;

        public static UserChampionshipsProgress Instance => _instance ??
                (_instance = new UserChampionshipsProgress(Path.Combine(FileUtils.GetDocumentsDirectory(), "launcherdata", "filestore", "champs.ini")));

        private readonly string _filename;
        private IDisposable _watcher;

        private UserChampionshipsProgress(string filename) {
            _filename = filename;
            _watcher = KunosLauncherDataWatcher.Subscribe(_filename, () => {
                Reload().Forget();
            }, () => {
                lock (_ignoreChangesSync) {
                    return (DateTime.Now - _ignoreChanges).TotalSeconds < 1d;
                }
            });

            if (!TryToLoad()) {
                Reset();
            }
        }

        private readonly object _ignoreChangesSync = new object();
        private DateTime _ignoreChanges;
        private string _current;

        [CanBeNull]
        internal string Current {
            get { return _current; }
            set {
                if (Equals(value, _current)) return;
                _current = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _aiLevel;

        internal double AiLevel {
            get { return _aiLevel; }
            set {
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private Dictionary<string, UserChampionshipProgressEntry> _entries;

        internal IReadOnlyDictionary<string, UserChampionshipProgressEntry> Entries {
            get { return _entries; }
            set {
                if (Equals(value, _entries)) return;
                _entries = (Dictionary<string, UserChampionshipProgressEntry>)value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        internal void UpdateEntry([NotNull] string id, [NotNull] UserChampionshipProgressEntry entry, bool globalUpdate) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            id = id.ToLowerInvariant();
            var data = Entries.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Key.Equals(id, StringComparison.OrdinalIgnoreCase) ? entry : x.Value);
            if (!data.ContainsKey(id)) {
                data[id] = entry;
            }

            if (globalUpdate) {
                Entries = data;
            } else {
                _entries = data;
                SaveLater();
            }
        }

        private void Reset() {
            Current = null;
            AiLevel = 95d;
            Entries = new Dictionary<string, UserChampionshipProgressEntry>(0);
        }

        private async Task Reload() {
            for (var i = 0; i < 3; i++) {
                if (TryToLoad()) return;
                await Task.Delay(300);
            }

            if (!TryToLoad()) {
                Reset();
            }
        }

        private bool TryToLoad() {
            _skipSaving = true;

            try {
                IniFile iniFile;
                try {
                    iniFile = new IniFile(_filename);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t load Kunos career progress", e);
                    return false;
                }
                
                Current = iniFile["CAREER"].GetNonEmpty("CURRENT");
                AiLevel = iniFile["CAREER"].GetDouble("AI_LEVEL", 95d);
                Entries = iniFile.Where(x => x.Key != "CHAMPS").ToDictionary(
                        x => x.Key.ToLowerInvariant(),
                        x => new UserChampionshipProgressEntry(
                                x.Value.GetInt("EVENT", 0),
                                x.Value.Select(y => new {
                                    Key = y.Key.StartsWith(@"EVENT") ? FlexibleParser.TryParseInt(y.Key.Substring(5)) : null as int?,
                                    y.Value
                                }).Where(y => y.Key.HasValue).ToDictionary(y => y.Key.Value, y => FlexibleParser.ParseInt(y.Value, 0)),
                                x.Value.GetIntNullable("POINTS"),
                                x.Value.Select(y => new {
                                    Key = y.Key.StartsWith(@"AI") ? FlexibleParser.TryParseInt(y.Key.Substring(2)) - 1 : null as int?,
                                    y.Value
                                }).Where(y => y.Key.HasValue).ToDictionary(y => y.Key.Value, y => FlexibleParser.ParseInt(y.Value, 0))));

                return true;
            } finally {
                _skipSaving = false;
            }
        }

        private void Save() {
            var iniFile = new IniFile(_filename) {
                ["CAREER"] = {
                    ["CURRENTSERIES"] = Current,
                    ["AI_LEVEL"] = AiLevel
                }
            };

            foreach (var pair in Entries) {
                var section = iniFile[pair.Key.ToUpperInvariant()];
                section.Clear();
                
                section["EVENT"] = pair.Value.SelectedEvent;

                if (pair.Value.Points.HasValue) {
                    section["POINTS"] = pair.Value.Points;
                } else {
                    section.Remove(@"POINTS");
                }

                foreach (var result in pair.Value.EventsResults.Where(x => x.Value != 0)) {
                    section[$"EVENT{result.Key}"] = result.Value;
                }

                foreach (var result in pair.Value.AiPoints.Where(x => x.Value != 0)) {
                    section[$"AI{result.Key + 1}"] = result.Value;
                }
            }

            if (File.Exists(_filename) && !File.Exists(_filename + ".backup")) {
                File.Copy(_filename, _filename + ".backup");
            }

            lock (_ignoreChangesSync) {
                _ignoreChanges = DateTime.Now;
            }

            iniFile.Save();
        }

        private bool _skipSaving, _savingInProgress;

        private async void SaveLater() {
            if (_savingInProgress || _skipSaving) return;
            _savingInProgress = true;

            await Task.Delay(300);

            Save();
            _savingInProgress = false;
        }

        public static void SaveBeforeExit() {
            if (_instance == null || !_instance._savingInProgress) return;
            _instance.Save();
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _watcher);
        }
    }
}