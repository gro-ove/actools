using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.PresetsPerMode {
    public sealed class ControlsPresetEntry : CustomPresetEntryBase {
        public ControlsPresetEntry(bool enabled, [CanBeNull] string filename) : base(enabled, filename) { }

        protected override string GetPresetName(string filename) {
            return ControlsSettings.GetCurrentPresetName(filename);
        }
    }

    public sealed class CustomPresetEntry : CustomPresetEntryBase {
        private readonly PresetsCategory _presetsCategory;

        public CustomPresetEntry(bool enabled, [CanBeNull] string filename, PresetsCategory presetsCategory) : base(enabled, filename) {
            _presetsCategory = presetsCategory;
        }

        protected override string GetPresetName(string filename) {
            return FileUtils.GetRelativePath(filename, PresetsManager.Instance.GetDirectory(_presetsCategory))
                            .ApartFromLast(_presetsCategory.Extension);
        }
    }

    public abstract class CustomPresetEntryBase : NotifyPropertyChanged {
        protected CustomPresetEntryBase(bool enabled, [CanBeNull] string filename) {
            _isEnabled = enabled;
            _filename = filename;
        }

        protected abstract string GetPresetName(string filename);

        public bool IsActuallyEnabled() {
            return IsEnabled && !string.IsNullOrWhiteSpace(Filename);
        }

        private bool _isEnabled;

        public bool IsEnabled {
            get => _isEnabled;
            set {
                if (Equals(value, IsEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _name;

        [CanBeNull]
        public string Name {
            get {
                if (_filename != null && _name == null) {
                    _name = GetPresetName(_filename);
                }
                return _name;
            }
            private set {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        private string _filename;

        [CanBeNull]
        public string Filename {
            get => _filename;
            set {
                if (Equals(value, Filename)) return;

                _filename = value;
                _name = null;

                OnPropertyChanged();
                OnPropertyChanged(nameof(Name));
            }
        }

        public object Value {
            get => null;
            set {
                var entry = value as ISavedPresetEntry;
                if (entry != null) {
                    Filename = entry.Filename;
                }
            }
        }

        public void Import(JObject jObject) {
            IsEnabled = jObject?.GetBoolValueOnly("enabled") ?? false;
            Filename = jObject?.GetStringValueOnly("filename");
        }

        public JObject Export() {
            return new JObject {
                ["enabled"] = IsEnabled,
                ["filename"] = Filename
            };
        }

        public void CopyFrom(CustomPresetEntryBase ext) {
            IsEnabled = ext.IsEnabled;
            Filename = ext.Filename;
        }
    }

    public abstract class PresetPerModeBase : NotifyPropertyChanged {
        public CustomPresetEntryBase Apps { get; } = new CustomPresetEntry(false, null, AcSettingsHolder.AppsPresetsCategory);
        public CustomPresetEntryBase Audio { get; } = new CustomPresetEntry(false, null, AcSettingsHolder.AudioPresetsCategory);
        public CustomPresetEntryBase Video { get; } = new CustomPresetEntry(false, null, AcSettingsHolder.VideoPresetsCategory);
        public CustomPresetEntryBase Controls { get; } = new ControlsPresetEntry(false, null);

        private bool? _rearViewMirror;

        public bool? RearViewMirror {
            get => _rearViewMirror;
            set {
                if (Equals(value, _rearViewMirror)) return;
                _rearViewMirror = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PresetPerMode.Serialized));
            }
        }
    }

    public class PresetsPerModeBackup : IDisposable {
        private static string BackupFilename => FilesStorage.Instance.GetTemporaryFilename("PPM Backup.json");

        public static void Revert() {
            var filename = BackupFilename;

            try {
                if (!File.Exists(filename)) return;

                var backup = JsonConvert.DeserializeObject<Backup>(File.ReadAllText(filename));

                if (backup.Apps != null) {
                    AcSettingsHolder.AppsPresets.ImportFromPresetData(backup.Apps);
                }

                if (backup.Audio != null) {
                    AcSettingsHolder.AudioPresets.ImportFromPresetData(backup.Audio);
                }

                if (backup.Video != null) {
                    AcSettingsHolder.VideoPresets.ImportFromPresetData(backup.Video);
                }

                if (backup.Controls != null) {
                    File.WriteAllText(AcSettingsHolder.Controls.Filename, backup.Controls);
                }

                if (backup.RearViewMirror != null) {
                    AcSettingsHolder.Gameplay.DisplayMirror = backup.RearViewMirror.Value;
                }
            } catch (Exception e) {
                throw new InformativeException("Can’t restore presets after setting mode-specific ones", e);
            }

            try {
                File.Delete(filename);
            } catch (Exception e) {
                throw new InformativeException("Can’t remove backup", e);
            }
        }

        private class Backup {
            public string Apps, Audio, Video, Controls;
            public bool? RearViewMirror;
        }

        public PresetsPerModeBackup(PresetPerModeBase set) {
            var backup = new Backup();

            if (set.Apps.IsActuallyEnabled()) {
                backup.Apps = AcSettingsHolder.AppsPresets.ExportToPresetData();
            }

            if (set.Audio.IsActuallyEnabled()) {
                backup.Audio = AcSettingsHolder.AudioPresets.ExportToPresetData();
            }

            if (set.Video.IsActuallyEnabled()) {
                backup.Video = AcSettingsHolder.VideoPresets.ExportToPresetData();
            }

            if (set.Controls.IsActuallyEnabled()) {
                try {
                    backup.Controls = File.ReadAllText(AcSettingsHolder.Controls.Filename);
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            if (set.RearViewMirror.HasValue) {
                backup.RearViewMirror = set.RearViewMirror.Value;
            }

            try {
                File.WriteAllText(BackupFilename, JsonConvert.SerializeObject(backup));
            } catch (Exception e) {
                throw new InformativeException("Can’t make a backup before loading presets for mode", e);
            }
        }

        public void Dispose() {
            Revert();
        }
    }

    public class PresetPerModeCombined : PresetPerModeBase {
        private bool _notEmpty;

        public bool NotEmpty {
            get => _notEmpty;
            private set {
                if (Equals(value, _notEmpty)) return;
                _notEmpty = value;
                OnPropertyChanged();
            }
        }

        public void Extend(PresetPerModeBase ext) {
            foreach (var p in GetType().GetProperties().Where(x => x.PropertyType == typeof(CustomPresetEntryBase))) {
                var e = (CustomPresetEntryBase)p.GetValue(ext);
                if (e.IsActuallyEnabled()) {
                    NotEmpty = true;
                    ((CustomPresetEntryBase)p.GetValue(this)).CopyFrom(e);
                }
            }

            RearViewMirror = ext.RearViewMirror ?? RearViewMirror;
        }

        [CanBeNull]
        private static IDisposable Apply(CustomPresetEntryBase entry, IUserPresetable presetable, string name, bool updateIfNeeded) {
            if (entry.IsActuallyEnabled() && entry.Filename != null) {
                Logging.Debug($"Applying {name} preset {entry.Filename}…");
                if (File.Exists(entry.Filename)) {
                    var data = File.ReadAllText(entry.Filename);
                    if (updateIfNeeded && SettingsHolder.Drive.PresetsPerModeAutoUpdate) {
                        presetable.ImportFromPresetData(data);
                        return new ActionAsDisposable(() => {
                            try {
                                var newData = presetable.ExportToPresetData();
                                if (newData != data) {
                                    File.WriteAllText(entry.Filename, newData);
                                    Logging.Debug($"Preset {name} updated");
                                }
                            } catch (Exception e) {
                                NonfatalError.NotifyBackground("Can’t update preset", e);
                            }
                        });
                    } else {
                        presetable.ImportFromPresetData(data);
                    }
                } else {
                    NonfatalError.NotifyBackground($"Can’t load {name} preset", $"File “{entry.Name}” is missing.");
                }
            }

            return null;
        }

        [CanBeNull]
        private static IDisposable ApplyWithUpdate(CustomPresetEntryBase entry, IUserPresetable presetable, string name) {
            return Apply(entry, presetable, name, true);
        }

        private static void Apply(CustomPresetEntryBase entry, IUserPresetable presetable, string name) {
            Apply(entry, presetable, name, false);
        }

        private static void Apply(CustomPresetEntryBase entry, string destination, string name) {
            if (entry.IsActuallyEnabled() && entry.Filename != null) {
                Logging.Debug($"Applying {name} preset {entry.Filename}…");
                if (File.Exists(entry.Filename)) {
                    File.Copy(entry.Filename, destination, true);
                } else {
                    NonfatalError.NotifyBackground($"Can’t load {name} preset", $"File “{entry.Name}” is missing.");
                }
            }
        }

        [CanBeNull]
        public IDisposable Apply() {
            if (!NotEmpty) return null;

            var backup = new PresetsPerModeBackup(this);

            var apps = ApplyWithUpdate(Apps, AcSettingsHolder.AppsPresets, "apps");
            Apply(Audio, AcSettingsHolder.AudioPresets, "audio");
            Apply(Video, AcSettingsHolder.VideoPresets, "video");
            Apply(Controls, AcSettingsHolder.Controls.Filename, "controls");

            if (RearViewMirror.HasValue) {
                AcSettingsHolder.Gameplay.DisplayMirror = RearViewMirror.Value;
            }

            // We need apps to be disposed first: when disposing, it will update preset is settings
            // changed, so we must revert them after that check.
            return apps.Join(backup);
        }
    }

    public class PresetPerMode : PresetPerModeBase, IDraggable {
        public PresetPerMode() {
            Initialize();
        }

        public PresetPerMode(string serialized) {
            Load(serialized);
            Initialize();
        }

        private void Initialize() {
            Controls.PropertyChanged += OnPropertyChanged;
            Apps.PropertyChanged += OnPropertyChanged;
            Audio.PropertyChanged += OnPropertyChanged;
            Video.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(CustomPresetEntryBase.Name)) {
                OnPropertyChanged(nameof(Serialized));
            }
        }

        private string _conditionId;

        public string ConditionId {
            get => _conditionId;
            set {
                if (Equals(value, _conditionId)) return;
                _conditionId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Serialized));
                OnConditionChanged();
            }
        }

        private string _conditionFn;

        public string ConditionFn {
            get => _conditionFn;
            set {
                if (value == _conditionFn) return;
                _conditionFn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Serialized));
                OnConditionChanged();
            }
        }

        public virtual void OnConditionChanged() { }

        private bool _enabled = true;

        public bool Enabled {
            get => _enabled;
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Serialized));
            }
        }

        private bool _deleted;

        public bool Deleted {
            get => _deleted;
            set {
                if (Equals(value, _deleted)) return;
                _deleted = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            Deleted = true;
        }));

        private void Load(string serialized) {
            var jObject = JObject.Parse(serialized);
            ConditionId = jObject.GetStringValueOnly("id");
            ConditionFn = jObject.GetStringValueOnly("fn");
            Enabled = jObject.GetBoolValueOnly("enabled") ?? true;
            RearViewMirror = jObject.GetBoolValueOnly("mirror");
            Controls.Import(jObject["controls"] as JObject);
            Apps.Import(jObject["apps"] as JObject);
            Audio.Import(jObject["audio"] as JObject);
            Video.Import(jObject["video"] as JObject);
        }

        public string Serialized => new JObject {
            ["id"] = ConditionId,
            ["fn"] = ConditionFn,
            ["enabled"] = Enabled,
            ["mirror"] = RearViewMirror,
            ["controls"] = Controls.Export(),
            ["apps"] = Apps.Export(),
            ["audio"] = Audio.Export(),
            ["video"] = Video.Export()
        }.ToString(Formatting.None);

        public static readonly string DraggableFormat = "X-PresetPerMode";

        string IDraggable.DraggableFormat => DraggableFormat;
    }

    public abstract class PresetsPerModeBase : NotifyPropertyChanged {
        private const string DefaultKey = "PresetsPerMode.sd";

        public class SaveableData {
            public List<string> Entries;
        }

        protected PresetsPerModeBase() {
            Saveable = new SaveHelper<SaveableData>(DefaultKey, () => new SaveableData {
                Entries = GetEntries().Select(x => x.Serialized).ToList()
            }, o => {
                SetEntries(o.Entries.Select(CreateEntry));
            }, () => {
                SetEntries(null);
            });
        }

        protected virtual PresetPerMode CreateEntry(string serialized) {
            return new PresetPerMode(serialized);
        }

        protected readonly ISaveHelper Saveable;

        protected abstract void SetEntries([CanBeNull] IEnumerable<PresetPerMode> entries);

        public abstract IEnumerable<PresetPerMode> GetEntries();
    }

    public class PresetsPerModeReadOnly : PresetsPerModeBase {
        private List<PresetPerMode> _entries;

        public PresetsPerModeReadOnly() {
            Saveable.Initialize();
        }

        protected override void SetEntries(IEnumerable<PresetPerMode> entries) {
            _entries = entries?.ToList();
        }

        public override IEnumerable<PresetPerMode> GetEntries() {
            if (_entries == null) return new PresetPerMode[0];
            return _entries;
        }
    }
}
