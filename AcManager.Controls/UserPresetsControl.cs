using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class UserPresetsControl : Control, IHierarchicalItemPreviewProvider {
        public static bool OptionSmartChangedHandling = true;

        private static readonly Dictionary<string, WeakReference<UserPresetsControl>> Instances =
                new Dictionary<string, WeakReference<UserPresetsControl>>();

        private static event EventHandler<ChangedPresetEventArgs> PresetSelected;

        static UserPresetsControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UserPresetsControl), new FrameworkPropertyMetadata(typeof(UserPresetsControl)));
        }

        public UserPresetsControl() {
            SaveCommand = new RelayCommand(SaveExecute, SaveCanExecute);
            Loaded += UserPresetsControl_Loaded;
            Unloaded += UserPresetsControl_Unloaded;
        }

        [CanBeNull]
        public static string GetCurrentFilename(string key) {
            return ValuesStorage.GetString("__userpresets_p_" + key);
        }

        private static UserPresetsControl GetInstance(string key) {
            WeakReference<UserPresetsControl> r;
            UserPresetsControl c;
            return Instances.TryGetValue(key, out r) && r.TryGetTarget(out c) ? c : null;
        }

        public static bool HasInstance(string key) {
            return GetInstance(key) != null;
        }

        public static bool LoadPreset(string key, string filename) {
            ValuesStorage.Set("__userpresets_p_" + key, filename);
            ValuesStorage.Set("__userpresets_c_" + key, false);

            var c = GetInstance(key);
            if (c == null) return false;

            c.UpdateSavedPresets();

            var entry = c.SavedPresets.FirstOrDefault(x => x.Filename == filename);
            if (entry == null) {
                Logging.Warning($@"[UserPresetsControl] Can’t set preset to “{filename}”, entry not found");
            } else if (c.CurrentUserPreset != entry) {
                c.CurrentUserPreset = entry;
            } else {
                c.SelectionChanged(entry);
            }

            return true;
        }

        public static bool LoadSerializedPreset(string key, string serialized) {
            ValuesStorage.Remove("__userpresets_p_" + key);
            ValuesStorage.Set("__userpresets_c_" + key, false);

            var c = GetInstance(key);
            if (c == null) return false;

            c.CurrentUserPreset = null;
            c.UserPresetable?.ImportFromPresetData(serialized);
            return true;
        }

        private bool _loaded;

        private void UserPresetsControl_Loaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            PresetSelected += UserPresetsControl_PresetSelected;
        }

        private void UserPresetsControl_Unloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            PresetSelected -= UserPresetsControl_PresetSelected;
        }

        private void UserPresetsControl_PresetSelected(object sender, ChangedPresetEventArgs args) {
            if (ReferenceEquals(sender, this) || _presetable == null || _presetable.PresetableKey != args.Key) return;
            SwitchTo(args.Value);
        }

        private string _selectedPresetFilename;

        public string SelectedPresetFilename {
            get {
                return _selectedPresetFilename ?? ValuesStorage.GetString("__userpresets_p_" + _presetable.PresetableKey)
                        ?? string.Empty;
            }
            private set {
                if (Equals(value, _selectedPresetFilename)) return;
                _selectedPresetFilename = value;
                if (_presetable != null) {
                    ValuesStorage.Set("__userpresets_p_" + _presetable.PresetableKey, value);
                }
            }
        }

        public static readonly DependencyProperty ShowSaveButtonProperty = DependencyProperty.Register(nameof(ShowSaveButton), typeof(bool),
                typeof(UserPresetsControl), new FrameworkPropertyMetadata(true));

        public bool ShowSaveButton {
            get { return (bool)GetValue(ShowSaveButtonProperty); }
            set { SetValue(ShowSaveButtonProperty, value); }
        }

        public static readonly DependencyProperty CurrentUserPresetProperty = DependencyProperty.Register("CurrentUserPreset", typeof(ISavedPresetEntry),
            typeof(UserPresetsControl), new PropertyMetadata(OnCurrentUserPresetChanged));

        private static void OnCurrentUserPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((UserPresetsControl)d).SelectionChanged((ISavedPresetEntry)e.NewValue);
        }

        public ISavedPresetEntry CurrentUserPreset {
            get { return (ISavedPresetEntry)GetValue(CurrentUserPresetProperty); }
            set { SetValue(CurrentUserPresetProperty, value); }
        }

        private bool _ignoreNext, _partiallyIgnoreNext;

        private void SelectionChanged(ISavedPresetEntry entry) {
            if (_ignoreNext) {
                _ignoreNext = false;
                return;
            }

            if (entry == null) return;
            SelectedPresetFilename = entry.Filename;
            _currentUserPresetData = null;

            if (!_partiallyIgnoreNext && _presetable != null) {
                PresetSelected?.Invoke(this, new ChangedPresetEventArgs(_presetable.PresetableKey, entry.ToString()));

                try {
                    _presetable.ImportFromPresetData(entry.ReadData());
                } catch (Exception) {
                    return; // TODO: Informing
                }
            }

            SetChanged(false);
        }

        private void SwitchTo(string value) {
            _partiallyIgnoreNext = true;
            CurrentUserPreset = SavedPresets.FirstOrDefault(x => x.ToString() == value);
            _partiallyIgnoreNext = false;
        }

        public ICommand SaveCommand { get; set; }

        public bool SaveCanExecute(object parameter) {
            return _presetable != null && _presetable.CanBeSaved;
        }

        public void SaveExecute(object parameter) {
            if (_presetable == null) return;

            var entry = CurrentUserPreset;
            string resultFilename;
            if (!PresetsManager.Instance.SavePresetUsingDialog(_presetable.PresetableCategory,
                                                               _presetable.ExportToPresetData(),
                                                               entry?.Filename,
                                                               out resultFilename)) return;
            SelectedPresetFilename = resultFilename;
            SetChanged(false);
        }

        public void SwitchToNext() {
            var presets = SavedPresets;
            
            var selectedId = presets.FindIndex(x => x.Filename == SelectedPresetFilename);
            if (selectedId == -1) {
                var defaultPreset = _presetable.DefaultPreset;
                CurrentUserPreset = presets.FirstOrDefault(x => x.DisplayName == defaultPreset) ?? presets.FirstOrDefault();
            } else if (++selectedId >= presets.Count) {
                CurrentUserPreset = presets.FirstOrDefault();
            } else {
                CurrentUserPreset = presets.ElementAtOrDefault(selectedId);
            }
        }

        public void SwitchToPrevious() {
            var presets = SavedPresets;

            var selectedId = presets.FindIndex(x => x.Filename == SelectedPresetFilename);
            if (selectedId == -1) {
                var defaultPreset = _presetable.DefaultPreset;
                CurrentUserPreset = presets.FirstOrDefault(x => x.DisplayName == defaultPreset) ?? presets.FirstOrDefault();
            } else if (--selectedId < 0) {
                CurrentUserPreset = presets.LastOrDefault();
            } else {
                CurrentUserPreset = presets.ElementAtOrDefault(selectedId);
            }
        }

        public static readonly DependencyPropertyKey PreviewProviderPropertyKey = DependencyProperty.RegisterReadOnly(nameof(PreviewProvider), typeof(IPresetsPreviewProvider),
                typeof(UserPresetsControl), new PropertyMetadata(null));

        public static readonly DependencyProperty PreviewProviderProperty = PreviewProviderPropertyKey.DependencyProperty;

        public IPresetsPreviewProvider PreviewProvider => (IPresetsPreviewProvider)GetValue(PreviewProviderProperty);

        public static readonly DependencyProperty UserPresetableProperty = DependencyProperty.Register("UserPresetable", typeof(IUserPresetable),
            typeof(UserPresetsControl), new PropertyMetadata(OnUserPresetableChanged));

        private static void OnUserPresetableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((UserPresetsControl)d).OnUserPresetableChanged((IUserPresetable)e.OldValue, (IUserPresetable)e.NewValue);
        }

        private static readonly DependencyPropertyKey SavedPresetsPropertyKey = DependencyProperty.RegisterReadOnly("SavedPresets", 
            typeof(ObservableCollection<ISavedPresetEntry>), typeof(UserPresetsControl), null);
        public static readonly DependencyProperty SavedPresetsProperty = SavedPresetsPropertyKey.DependencyProperty;

        public ObservableCollection<ISavedPresetEntry> SavedPresets => (ObservableCollection<ISavedPresetEntry>)GetValue(SavedPresetsProperty);

        private static readonly DependencyPropertyKey SavedPresetsGroupedPropertyKey = DependencyProperty.RegisterReadOnly("SavedPresetsGrouped", 
            typeof(HierarchicalGroup), typeof(UserPresetsControl), null);
        public static readonly DependencyProperty SavedPresetsGroupedProperty = SavedPresetsGroupedPropertyKey.DependencyProperty;

        public HierarchicalGroup SavedPresetsGrouped => (HierarchicalGroup)GetValue(SavedPresetsGroupedProperty);

        private IUserPresetable _presetable;

        private void OnUserPresetableChanged(IUserPresetable oldValue, IUserPresetable newValue) {
            if (oldValue != null) {
                Instances.Remove(oldValue.PresetableKey);
            }

            Instances.RemoveDeadReferences();

            if (newValue != null) {
                Instances[newValue.PresetableKey] = new WeakReference<UserPresetsControl>(this);
            }

            if (_presetable != null) {
                PresetsManager.Instance.Watcher(_presetable.PresetableCategory).Update -= Presets_Update;
                _presetable.Changed -= Presetable_Changed;
            }

            _presetable = newValue;
            SetValue(PreviewProviderPropertyKey, _presetable as IPresetsPreviewProvider);
            if (_presetable == null) return;

            PresetsManager.Instance.Watcher(_presetable.PresetableCategory).Update += Presets_Update;
            _presetable.Changed += Presetable_Changed;
            UpdateSavedPresets();
            SetChanged();
        }

        private static string GetHead(string filename, string from) {
            for (int i = filename.LastIndexOf(Path.DirectorySeparatorChar), t; i > 0; i = t) {
                t = filename.LastIndexOf(Path.DirectorySeparatorChar, i - 1);
                if (t <= from.Length + 1) {
                    return filename.Substring(0, i);
                }
            }
            return from;
        }

        public static IEnumerable<object> GroupPresets(IEnumerable<ISavedPresetEntry> entries, string mainDirectory) {
            var list = entries.Select(x => new {
                Entry = x,
                Directory = GetHead(x.Filename, mainDirectory)
            }).ToList();

            foreach (var directory in list.Select(x => x.Directory).Where(x => x != mainDirectory).Distinct()) {
                var directoryValue = directory;
                var subList = list.Where(x => x.Directory == directoryValue).Select(x => x.Entry).ToList();
                if (subList.Count > 1){
                    yield return new HierarchicalGroup(Path.GetFileName(directory), GroupPresets(subList, directory));
                } else if (list.Any()) {
                    yield return subList[0];
                }
            }

            foreach (var entry in list.Where(x => x.Directory == mainDirectory)) {
                yield return entry.Entry;
            }
        }

        public static IEnumerable<object> GroupPresets(string presetableKey) {
            return GroupPresets(PresetsManager.Instance.GetSavedPresets(presetableKey), PresetsManager.Instance.GetDirectory(presetableKey));
        }

        private void UpdateSavedPresets() {
            if (_presetable == null) return;

            var presets = new ObservableCollection<ISavedPresetEntry>(PresetsManager.Instance.GetSavedPresets(_presetable.PresetableCategory));
            SetValue(SavedPresetsPropertyKey, presets);
            SetValue(SavedPresetsGroupedPropertyKey, new HierarchicalGroup("",
                    GroupPresets(presets, PresetsManager.Instance.GetDirectory(_presetable.PresetableCategory))));
            
            _ignoreNext = true;
            var defaultPreset = _presetable.DefaultPreset;
            CurrentUserPreset = presets.FirstOrDefault(x => x.Filename == SelectedPresetFilename) ??
                    (defaultPreset == null ? null : presets.FirstOrDefault(x => x.DisplayName == defaultPreset));
            _ignoreNext = false;
        }

        private static readonly DependencyPropertyKey ChangedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Changed), 
            typeof(bool), typeof(UserPresetsControl), null);
        public static readonly DependencyProperty ChangedProperty = ChangedPropertyKey.DependencyProperty;

        public bool Changed => (bool)GetValue(ChangedProperty);

        private void SetChanged(bool? value = null) {
            if (_presetable == null || Changed == value) return;

            if (value == true && _comboBox != null) {
                _comboBox.SelectedItem = null;
            }

            SetValue(ChangedPropertyKey, value ?? ValuesStorage.GetBool("__userpresets_c_" + _presetable.PresetableKey));
            if (value.HasValue) {
                ValuesStorage.Set("__userpresets_c_" + _presetable.PresetableKey, value.Value);
            }
        }

        private string _currentUserPresetData;

        private void Presetable_Changed(object sender, EventArgs e) {
            if (OptionSmartChangedHandling) {
                if (_currentUserPresetData == null) {
                    _currentUserPresetData = CurrentUserPreset?.ReadData();
                }

                var actualData = _presetable.ExportToPresetData();
                SetChanged(actualData != _currentUserPresetData);
            } else {
                SetChanged(true);
            }
        }

        private void Presets_Update(object sender, EventArgs e) {
            UpdateSavedPresets();
        }

        private HierarchicalComboBox _comboBox;

        public override void OnApplyTemplate() {
            if (_comboBox != null) {
                _comboBox.SelectionChanged -= ComboBox_SelectionChanged;
                _comboBox.PreviewProvider = null;
            }

            _comboBox = GetTemplateChild(@"PART_ComboBox") as HierarchicalComboBox;
            if (_comboBox != null) {
                _comboBox.SelectionChanged += ComboBox_SelectionChanged;
                _comboBox.PreviewProvider = this;
            }

            base.OnApplyTemplate();
        }

        private void ComboBox_SelectionChanged(object sender, SelectedItemChangedEventArgs e) {
            var entry = _comboBox?.SelectedItem as ISavedPresetEntry;
            if (entry == null) return;

            if (CurrentUserPreset != entry) {
                CurrentUserPreset = entry;
            } else {
                SelectionChanged(entry);
            }
        }

        public IUserPresetable UserPresetable {
            get { return (IUserPresetable)GetValue(UserPresetableProperty); }
            set { SetValue(UserPresetableProperty, value); }
        }

        public object GetPreview(object item) {
            var p = _presetable as IPresetsPreviewProvider;
            if (p == null) return null;

            var data = (item as ISavedPresetEntry)?.ReadData();
            if (data == null) return null;

            return p.GetPreview(data);
        }
    }
}
