using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class TrackStatesHelper : IDisposable {
        private readonly string _templates;
        private readonly FileSystemWatcher _watcher;

        private TrackStatesHelper() {
            _templates = Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "templates");
            RegisterBuiltInPresets();

            Directory.CreateDirectory(_templates);
            _watcher = new FileSystemWatcher {
                Path = _templates,
                Filter = "tracks.ini",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += Handler;
            _watcher.Created += Handler;
            _watcher.Deleted += Handler;
            _watcher.Renamed += Handler;
        }

        private bool _inProgress;

        private async Task ReloadLater() {
            if (_inProgress) return;

            try {
                _inProgress = true;

                for (var i = 0; i < 10; i++) {
                    try {
                        await Task.Delay(300);
                        (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(RegisterBuiltInPresets);
                        break;
                    } catch (IOException) { }
                }
            } finally {
                _inProgress = false;
            }
        }

        private void Handler(object sender, FileSystemEventArgs e) {
            ReloadLater().Forget();
        }

        private static readonly Regex NameRegex = new Regex(@"(?<=\b[A-Z])[A-Z]+", RegexOptions.Compiled);

        [NotNull]
        private static string NameFromId([NotNull] string id) {
            return NameRegex.Replace(id, x => x.Value.ToLower(CultureInfo.CurrentUICulture));
        }

        private IEnumerable<Tuple<string, TrackStateViewModelBase>> GetBuiltInPresets() {
            var filename = Path.Combine(_templates, "tracks.ini");
            var ini = new IniFile(filename);
            foreach (var pair in ini) {
                yield return Tuple.Create(NameFromId(pair.Key), TrackStateViewModelBase.CreateBuiltIn(pair.Value));
            }
        }

        private void RegisterBuiltInPresets() {
            PresetsManager.Instance.ClearBuiltInPresets(TrackStateViewModelBase.PresetableCategory);
            PresetsManager.Instance.RegisterBuiltInPreset(TrackStateViewModelBase.CreateBuiltIn(null).ToBytes(),
                    TrackStateViewModelBase.PresetableCategory, "Auto (set by weather)");

            foreach (var preset in GetBuiltInPresets()) {
                PresetsManager.Instance.RegisterBuiltInPreset(preset.Item2.ToBytes(), TrackStateViewModelBase.PresetableCategory, preset.Item1);
            }

            UserPresetsControl.RescanCategory(TrackStateViewModelBase.PresetableCategory, true);
        }

        public void Dispose() {
            _watcher.EnableRaisingEvents = false;
            _watcher?.Dispose();
        }

        public static TrackStatesHelper Instance { get; private set; }

        public static void Initialize() {
            Instance = new TrackStatesHelper();
        }
    }
}