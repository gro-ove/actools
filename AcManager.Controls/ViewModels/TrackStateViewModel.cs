using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Controls.ViewModels {
    /// <summary>
    /// Full version with presets. Load-save-switch between presets-save as a preset, full
    /// package. Also, provides previews for presets!
    /// </summary>
    public class TrackStateViewModel : TrackStateViewModelBase, IUserPresetable, IPresetsPreviewProvider {
        private static TrackStateViewModel _instance;

        public static TrackStateViewModel Instance => _instance ?? (_instance = new TrackStateViewModel("qdtrackstate"));

        public TrackStateViewModel([Localizable(false)] string customKey = null) : base(customKey, false) {
            PresetableKey = customKey ?? UserPresetableKeyValue;
            Saveable.Initialize();
        }

        protected override void SaveLater() {
            base.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        #region Presetable
        bool IUserPresetable.CanBeSaved => true;

        public string PresetableKey { get; }

        string IUserPresetable.PresetableCategory => UserPresetableKeyValue;

        string IUserPresetable.DefaultPreset => "Green";

        public string ExportToPresetData() {
            return Saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            Saveable.FromSerializedString(data);
        }

        object IPresetsPreviewProvider.GetPreview(string data) {
            return new UserControls.TrackStateDescription { DataContext = CreateFixed(data) };
        }
        #endregion
    }

    public class TrackStatesHelper : IDisposable {
        private readonly string _templates;
        private readonly FileSystemWatcher _watcher;

        private TrackStatesHelper() {
            _templates = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "templates");
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
                await Task.Delay(200);
                (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(RegisterBuiltInPresets);
            } finally {
                _inProgress = false;
            }
        }

        private void Handler(object sender, FileSystemEventArgs e) {
            ReloadLater().Forget();
        }

        private IEnumerable<Tuple<string, TrackStateViewModelBase>> GetBuiltInPresets() {
            var filename = Path.Combine(_templates, "tracks.ini");
            var ini = new IniFile(filename);
            foreach (var pair in ini) {
                yield return Tuple.Create(AcStringValues.NameFromId(pair.Key), TrackStateViewModelBase.CreateBuiltIn(pair.Value));
            }
        }

        private void RegisterBuiltInPresets() {
            PresetsManager.Instance.ClearBuiltInPresets(TrackStateViewModelBase.UserPresetableKeyValue);
            foreach (var preset in GetBuiltInPresets()) {
                PresetsManager.Instance.RegisterBuiltInPreset(preset.Item2.ToBytes(), TrackStateViewModelBase.UserPresetableKeyValue, preset.Item1);
            }
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