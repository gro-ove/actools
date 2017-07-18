using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.PresetsPerMode;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using Newtonsoft.Json;

namespace AcManager.Pages.AcSettings {
    public partial class PresetsPerMode {
        private ViewModel Model => (ViewModel)DataContext;

        public PresetsPerMode() {
            DataContext = new ViewModel();
            InitializeComponent();

            this.OnActualUnload(Unload);
        }

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class Mode : Displayable, IWithId {
            public string Id { get; }

            public string Script { get; }

            [JsonConstructor]
            public Mode(string id, string name, string script) {
                Id = id;
                DisplayName = name;
                Script = script;
            }

            private bool Equals(Mode other) {
                return string.Equals(Id, other.Id) && string.Equals(DisplayName, other.DisplayName) && string.Equals(Script, other.Script);
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Mode)obj));
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = Id?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ (DisplayName?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (Script?.GetHashCode() ?? 0);
                    return hashCode;
                }
            }
        }

        public class PresetPerModeUi : PresetPerMode {
            private readonly IEnumerable<Mode> _modes;

            public PresetPerModeUi(IEnumerable<Mode> modes) {
                _modes = modes;
                OnConditionChanged();
            }

            public PresetPerModeUi(string serialized, IEnumerable<Mode> modes) : base(serialized) {
                _modes = modes;
                OnConditionChanged();
            }

            private Mode _mode;

            public Mode Mode {
                get {
                    if (_mode == null) {
                        if (_modes == null) {
                            Logging.Unexpected();
                            _mode = new Mode(ConditionId, AcStringValues.NameFromId(ConditionId), ConditionFn);
                        } else {
                            var mode = _modes.GetByIdOrDefault(ConditionId);
                            if (mode != null) {
                                ConditionFn = mode.Script;
                            } else {
                                Logging.Warning($"Mode with ID=“{ConditionId}” missing");
                                mode = _modes.FirstOrDefault(x => x.Script == ConditionFn);
                                if (mode != null) {
                                    Logging.Warning($"Mode with ID=“{mode.Id}” will be used instead");
                                    ConditionId = mode.Id;
                                } else {
                                    Logging.Warning($"Mode with Func=“{ConditionFn}” missing as well");
                                    mode = new Mode(ConditionId, AcStringValues.NameFromId(ConditionId), ConditionFn);
                                }
                            }

                            _mode = mode;
                        }
                    }

                    return _mode;
                }
                set {
                    if (Equals(value, _mode)) return;
                    _mode = value;
                    OnPropertyChanged();

                    _ignore = true;
                    ConditionId = value?.Id;
                    ConditionFn = value?.Script;
                    _ignore = false;
                }
            }

            private bool _ignore;

            public sealed override void OnConditionChanged() {
                if (!_ignore && _modes != null) {
                    _mode = null;
                    OnPropertyChanged(nameof(Mode));
                }
            }
        }

        public class ViewModel : PresetsPerModeBase, IDisposable {
            public BetterObservableCollection<Mode> Modes { get; }

            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public HierarchicalGroup AppPresets { get; }

            public HierarchicalGroup AudioPresets { get; }

            public HierarchicalGroup VideoPresets { get; }

            public ViewModel() {
                Entries = new ChangeableObservableCollection<PresetPerMode>();
                Modes = new BetterObservableCollection<Mode>();

                AppPresets = new HierarchicalGroup("", UserPresetsControl.GroupPresets(new PresetsCategory(AcSettingsHolder.AppsPresetsKey)));
                AudioPresets = new HierarchicalGroup("", UserPresetsControl.GroupPresets(new PresetsCategory(AcSettingsHolder.AudioPresetsKey)));
                VideoPresets = new HierarchicalGroup("", UserPresetsControl.GroupPresets(new PresetsCategory(AcSettingsHolder.VideoPresetsKey)));

                UpdateModes();
                FilesStorage.Instance.Watcher(ContentCategory.PresetsPerModeConditions).Update += OnCategoriesUpdate;

                Saveable.Initialize();

                Entries.CollectionChanged += OnCollectionChanged;
                Entries.ItemPropertyChanged += OnItemPropertyChanged;
            }

            private void UpdateModes() {
                Modes.ReplaceIfDifferBy(FilesStorage.Instance.GetContentFilesFiltered(@"*.json", ContentCategory.PresetsPerModeConditions)
                                                    .SelectMany(x => {
                                                        try {
                                                            return JsonConvert.DeserializeObject<Mode[]>(File.ReadAllText(x.Filename));
                                                        } catch (Exception e) {
                                                            Logging.Warning($"Cannot load file {x.Name}: {e}");
                                                            return new Mode[0];
                                                        }
                                                    })
                                                    .OrderBy(x => x.DisplayName));
                foreach (var entry in Entries.OfType<PresetPerModeUi>()) {
                    entry.OnConditionChanged();
                }
            }

            protected override PresetPerMode CreateEntry(string serialized) {
                return new PresetPerModeUi(serialized, Modes);
            }

            private void OnCategoriesUpdate(object sender, EventArgs e) {
                UpdateModes();
            }

            protected void SaveLater() {
                Saveable.SaveLater();
            }

            private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(PresetPerMode.Deleted)) {
                    Entries.Remove((PresetPerMode)sender);
                } else {
                    SaveLater();
                }
            }

            private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                SaveLater();
            }

            public ChangeableObservableCollection<PresetPerMode> Entries { get; private set; }

            public void Dispose() {
                _helper.Dispose();
                FilesStorage.Instance.Watcher(ContentCategory.PresetsPerModeConditions).Update -= OnCategoriesUpdate;
            }

            protected override void SetEntries(IEnumerable<PresetPerMode> entries) {
                if (entries == null) {
                    Entries.Clear();
                } else {
                    Entries.ReplaceEverythingBy(entries);
                }
            }

            public override IEnumerable<PresetPerMode> GetEntries() {
                return Entries;
            }
        }

        private void Unload() {
            Model.Dispose();
        }

        private void OnAddNewRoundButtonClick(object sender, RoutedEventArgs e) {
            Model.Entries.Add(new PresetPerModeUi(Model.Modes));
        }
    }
}
