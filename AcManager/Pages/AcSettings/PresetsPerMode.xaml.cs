using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.PresetsPerMode;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
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

        public class PresetPerModeUi : PresetPerMode {
            public PresetPerModeUi() {
                OnConditionChanged();
            }

            public PresetPerModeUi(string serialized) : base(serialized) {
                OnConditionChanged();
            }

            private ModeSpecificPresetsHelper.Mode _mode;

            public ModeSpecificPresetsHelper.Mode Mode {
                get {
                    if (_mode == null) {
                        var mode = ModeSpecificPresetsHelper.GetModes().GetByIdOrDefault(ConditionId);
                        if (mode != null) {
                            ConditionFn = mode.Script;
                        } else {
                            Logging.Warning($"Mode with ID=“{ConditionId}” missing");
                            mode = ModeSpecificPresetsHelper.GetModes().FirstOrDefault(x => x.Script == ConditionFn);
                            if (mode != null) {
                                Logging.Warning($"Mode with ID=“{mode.Id}” will be used instead");
                                ConditionId = mode.Id;
                            } else {
                                Logging.Warning($"Mode with Func=“{ConditionFn}” missing as well");
                                mode = new ModeSpecificPresetsHelper.Mode(ConditionId, AcStringValues.NameFromId(ConditionId), ConditionFn);
                            }
                        }

                        _mode = mode;
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
                if (!_ignore) {
                    _mode = null;
                    OnPropertyChanged(nameof(Mode));
                }
            }
        }

        public class ViewModel : PresetsPerModeBase, IDisposable {
            public HierarchicalGroup Modes { get; }

            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public HierarchicalGroup AppPresets { get; }

            public HierarchicalGroup AudioPresets { get; }

            public HierarchicalGroup VideoPresets { get; }

            public HierarchicalGroup PatchPresets { get; }

            public ViewModel() {
                Entries = new ChangeableObservableCollection<PresetPerMode>();
                
                Modes = new HierarchicalGroup();
                foreach (var mode in ModeSpecificPresetsHelper.GetModes().GroupBy(x => x.Category).OrderBy(x => x.Key)) {
                    Modes.Add(new HierarchicalGroup(mode.Key, mode.OrderBy(x => x.DisplayName).Select(x => {
                        var ret = new HierarchicalItem { Header = x.DisplayName, ToolTip = x.Description };
                        HierarchicalItemsView.SetValue(ret, x);
                        return ret;
                    })));
                }

                AppPresets = new HierarchicalGroup("", UserPresetsControl.GroupPresets(new PresetsCategory(AcSettingsHolder.AppsPresetsKey)));
                AudioPresets = new HierarchicalGroup("", UserPresetsControl.GroupPresets(new PresetsCategory(AcSettingsHolder.AudioPresetsKey)));
                VideoPresets = new HierarchicalGroup("", UserPresetsControl.GroupPresets(new PresetsCategory(AcSettingsHolder.VideoPresetsKey)));
                PatchPresets = new HierarchicalGroup("", UserPresetsControl.GroupPresets(PatchSettingsModel.Category));

                FilesStorage.Instance.Watcher(ContentCategory.PresetsPerModeConditions).Update += OnCategoriesUpdate;
                Saveable.Initialize();

                Entries.CollectionChanged += OnCollectionChanged;
                Entries.ItemPropertyChanged += OnItemPropertyChanged;
            }

            private void UpdateModes() {
                foreach (var entry in Entries.OfType<PresetPerModeUi>()) {
                    entry.OnConditionChanged();
                }
            }

            protected override PresetPerMode CreateEntry(string serialized) {
                return new PresetPerModeUi(serialized);
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
            Model.Entries.Add(new PresetPerModeUi());
        }
    }
}
