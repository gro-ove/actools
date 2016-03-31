using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    /// <summary>
    /// Only for KunosCareerObject and KunosCareerEventObject
    /// </summary>
    public class KunosCareerProgress : NotifyPropertyChanged {
        private static KunosCareerProgress _instance;

        public static KunosCareerProgress Instance => _instance ?? (_instance = new KunosCareerProgress());

        public static void Initialize() {
            Debug.Assert(_instance == null);
            _instance = new KunosCareerProgress();
        }

        private readonly string _filename;

        private KunosCareerProgress() {
            _filename = FileUtils.GetKunosCareerProgressFilename();

            var fsWatcher = new FileSystemWatcher {
                Path = Path.GetDirectoryName(_filename),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = Path.GetFileName(_filename)
            };
            fsWatcher.Changed += FsWatcher_Changed;
            fsWatcher.Created += FsWatcher_Changed;
            fsWatcher.Deleted += FsWatcher_Changed;
            fsWatcher.Renamed += FsWatcher_Changed;
            fsWatcher.EnableRaisingEvents = true;

            Load();
        }

        private DateTime _ignoreChanges;

        private void FsWatcher_Changed(object sender, FileSystemEventArgs e) {
            if (DateTime.Now - _ignoreChanges < TimeSpan.FromSeconds(1)) return;
            LoadLater();
        }

        private bool _loadingInProgress;

        private async void LoadLater() {
            if (_loadingInProgress) return;
            _loadingInProgress = true;

            await Task.Delay(500);

            Application.Current.Dispatcher.Invoke(Load);
            _loadingInProgress = false;
        }

        private string _currentSeries;

        internal string CurrentSeries {
            get { return _currentSeries; }
            set {
                if (Equals(value, _currentSeries)) return;
                _currentSeries = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private string[] _completed;

        internal string[] Completed {
            get { return _completed; }
            set {
                if (Equals(value, _completed)) return;
                _completed = value;
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

        private bool _isNew;

        internal bool IsNew {
            get { return _isNew; }
            set {
                if (Equals(value, _isNew)) return;
                _isNew = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private Dictionary<string, KunosCareerProgressEntry> _entries;

        internal IReadOnlyDictionary<string, KunosCareerProgressEntry> Entries {
            get { return _entries; }
            private set {
                if (Equals(value, _entries)) return;
                _entries = (Dictionary<string, KunosCareerProgressEntry>)value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        internal void UpdateEntry([NotNull] string id, [NotNull] KunosCareerProgressEntry entry, bool globalUpdate) {
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

        private void Load() {
            _skipSaving = true;

            var iniFile = new IniFile(_filename);
            Completed = iniFile["CAREER"].GetStrings("COMPLETE").Select(x => x.ToLowerInvariant()).ToArray();
            CurrentSeries = iniFile["CAREER"].Get("CURRENTSERIES");
            AiLevel = iniFile["CAREER"].GetDouble("AI_LEVEL", 95d);
            IsNew = iniFile["CAREER"].GetInt("INTRO", 0) != 2;
            Entries = iniFile.Where(x => x.Key.StartsWith("SERIES")).ToDictionary(
                    x => x.Key.ToLowerInvariant(),
                    x => new KunosCareerProgressEntry(
                        x.Value.GetInt("EVENT", 0),
                        Enumerable.Range(0, 999)
                                .Select(y => $"EVENT{y}")
                                .TakeWhile(y => x.Value.ContainsKey(y))
                                .Select(y => x.Value.GetInt(y, 0)).ToArray(),
                        x.Value.GetIntNullable("POINTS"),
                        Enumerable.Range(1, 99)
                                .Select(y => $"AI{y}")
                                .TakeWhile(y => x.Value.ContainsKey(y))
                                .Select(y => x.Value.GetInt(y, 0)).ToArray()
,
                        x.Value.GetLong("LASTSELECTED", 0)));

            _skipSaving = false;
        }

        private void Save() {
            var iniFile = new IniFile(_filename) {
                ["CAREER"] = {
                    ["COMPLETE"] = Completed.JoinToString(','),
                    ["CURRENTSERIES"] = CurrentSeries,
                    ["AI_LEVEL"] = AiLevel,
                    ["INTRO"] = IsNew ? 0 : 2,
                }
            };

            foreach (var pair in Entries) {
                var section = iniFile[pair.Key.ToUpperInvariant()];
                section.Clear();

                section["LASTSELECTED"] = pair.Value.LastSelectedTimestamp;
                section["EVENT"] = pair.Value.SelectedEvent;

                if (pair.Value.Points.HasValue) {
                    section["POINTS"] = pair.Value.Points;
                } else {
                    section.Remove("POINTS");
                }

                for (var i = 0; i < pair.Value.EventsResults.Count; i++) {
                    section[$"EVENT{i}"] = pair.Value.EventsResults[i];
                }

                for (var i = 0; i < pair.Value.AiPoints?.Count; i++) {
                    section[$"AI{i + 1}"] = pair.Value.AiPoints[i];
                }
            }

            if (!File.Exists(_filename + ".backup")) {
                File.Copy(_filename, _filename + ".backup");
            }

            _ignoreChanges = DateTime.Now;
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
    }

    internal class KunosCareerProgressEntry {
        /// <summary>
        /// Milliseconds.
        /// </summary>
        internal readonly long LastSelectedTimestamp;

        /// <summary>
        /// Starts from 0.
        /// </summary>
        internal readonly int SelectedEvent;
        internal readonly IReadOnlyList<int> EventsResults;
        internal readonly int? Points;

        /// <summary>
        /// Starts with 0, of course.
        /// </summary>
        internal readonly IReadOnlyList<int> AiPoints;

        /// <summary>
        /// New instance.
        /// </summary>
        /// <param name="selectedEvent">Starts from 0</param>
        /// <param name="eventsResults"></param>
        /// <param name="points"></param>
        /// <param name="aiPoints"></param>
        /// <param name="lastSelectedTimestamp">Milliseconds</param>
        internal KunosCareerProgressEntry(int selectedEvent, IEnumerable<int> eventsResults, int? points, IEnumerable<int> aiPoints, long? lastSelectedTimestamp = null) {
            SelectedEvent = selectedEvent;
            EventsResults = eventsResults.ToIReadOnlyListIfItsNot();
            Points = points;
            AiPoints = aiPoints.ToIReadOnlyListIfItsNot();
            LastSelectedTimestamp = lastSelectedTimestamp ?? DateTime.Now.ToMillisecondsTimestamp();
        }
    }
}
