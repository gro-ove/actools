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
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class UserPresetsControl : Control {
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
            c.UserPresetable?.ImportFromUserPresetData(serialized);
            return true;
        }

        private void UserPresetsControl_Loaded(object sender, RoutedEventArgs e) {
            PresetSelected += UserPresetsControl_PresetSelected;
        }

        private void UserPresetsControl_Unloaded(object sender, RoutedEventArgs e) {
            PresetSelected -= UserPresetsControl_PresetSelected;
        }

        private void UserPresetsControl_PresetSelected(object sender, ChangedPresetEventArgs args) {
            if (ReferenceEquals(sender, this) || _presetable == null || _presetable.UserPresetableKey != args.Key) return;
            SwitchTo(args.Value);
        }

        private string _selectedPresetFilename;

        public string SelectedPresetFilename {
            get {
                return _selectedPresetFilename ?? ValuesStorage.GetString("__userpresets_p_" + _presetable.UserPresetableKey) 
                    ?? string.Empty;
            }
            private set {
                if (Equals(value, _selectedPresetFilename)) return;
                _selectedPresetFilename = value;
                if (_presetable != null) {
                    ValuesStorage.Set("__userpresets_p_" + _presetable.UserPresetableKey, value);
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
                PresetSelected?.Invoke(this, new ChangedPresetEventArgs(_presetable.UserPresetableKey, entry.ToString()));

                try {
                    _presetable.ImportFromUserPresetData(entry.ReadData());
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
            if (!PresetsManager.Instance.SavePresetUsingDialog(_presetable.UserPresetableKey,
                                                               _presetable.ExportToUserPresetData(),
                                                               entry?.Filename,
                                                               out resultFilename)) return;

            SetChanged(false);
            SelectedPresetFilename = resultFilename;
        }

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
            typeof(ObservableCollection<MenuItem>), typeof(UserPresetsControl), null);
        public static readonly DependencyProperty SavedPresetsGroupedProperty = SavedPresetsGroupedPropertyKey.DependencyProperty;

        public ObservableCollection<MenuItem> SavedPresetsGrouped => (ObservableCollection<MenuItem>)GetValue(SavedPresetsGroupedProperty);

        private IUserPresetable _presetable;

        private void OnUserPresetableChanged(IUserPresetable oldValue, IUserPresetable newValue) {
            if (oldValue != null) {
                Instances.Remove(oldValue.UserPresetableKey);
            }

            Instances.RemoveDeadReferences();

            if (newValue != null) {
                Instances[newValue.UserPresetableKey] = new WeakReference<UserPresetsControl>(this);
            }

            if (_presetable != null) {
                PresetsManager.Instance.Watcher(_presetable.UserPresetableKey).Update -= Presets_Update;
                _presetable.Changed -= Presetable_Changed;
            }

            _presetable = newValue;
            if (_presetable == null) return;

            PresetsManager.Instance.Watcher(_presetable.UserPresetableKey).Update += Presets_Update;
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

        public class TagHelper : NotifyPropertyChanged {
            private readonly IPreviewProvider _provider;

            internal TagHelper(IPreviewProvider provider, ISavedPresetEntry entry) {
                _provider = provider;
                Entry = entry;
            }

            public ISavedPresetEntry Entry { get; }

            public bool HasToolTips => _provider != null;

            private bool _previewReady;
            private object _toolTip;

            public object ToolTip {
                get {
                    if (_previewReady) return _toolTip;

                    _previewReady = true;
                    _toolTip = _provider?.GetPreview(Entry.ReadData());
                    return _toolTip;
                }
            }
        }

        private static MenuItem ToMenuItem(ISavedPresetEntry entry, string mainDirectory, RoutedEventHandler clickHandler,
                IPreviewProvider previewProvider, string extension) {
            try {
                var l = mainDirectory.Length + 1;
                var result = new MenuItem {
                    Header = entry.Filename.Substring(l, entry.Filename.Length - l - extension.Length),
                    Tag = new TagHelper(previewProvider, entry),
                };

                result.Click += clickHandler;
                return result;
            } catch (ArgumentOutOfRangeException e) {
                Logging.Warning("ToMenuItem() exception:\n" +
                        $"  mainDirectory: {mainDirectory}\n" +
                        $"  filename: {entry.Filename}\n" +
                        $"  extension: {extension}\n  " + e);
                
                var result = new MenuItem {
                    Header = entry.Filename,
                    Tag = new TagHelper(previewProvider, entry),
                };

                result.Click += clickHandler;
                return result;
            }
        }

        public static IEnumerable<MenuItem> GroupPresets(IEnumerable<ISavedPresetEntry> entries, string mainDirectory, RoutedEventHandler clickHandler,
                IPreviewProvider previewProvider = null, string extension = PresetsManager.FileExtension) {
            var list = entries.Select(x => new {
                Entry = x,
                Directory = GetHead(x.Filename, mainDirectory)
            }).ToList();

            foreach (var directory in list.Select(x => x.Directory).Where(x => x != mainDirectory).Distinct()) {
                var directoryValue = directory;
                var subList = list.Where(x => x.Directory == directoryValue).Select(x => x.Entry).ToList();
                if (subList.Count > 1){
                    var group = new MenuItem {
                        Header = Path.GetFileName(directory)
                    };

                    foreach (var sub in GroupPresets(subList, directory, clickHandler, previewProvider, extension)){
                        group.Items.Add(sub);
                    }

                    yield return group;
                } else if (list.Any()){
                    yield return ToMenuItem(subList[0], mainDirectory, clickHandler, previewProvider, extension);
                }
            }

            foreach (var entry in list.Where(x => x.Directory == mainDirectory)) {
                yield return ToMenuItem(entry.Entry, mainDirectory, clickHandler, previewProvider, extension);
            }
        }

        public static IEnumerable<MenuItem> GroupPresets(string presetableKey,
                RoutedEventHandler clickHandler,
                IPreviewProvider previewProvider = null) {
            return GroupPresets(PresetsManager.Instance.GetSavedPresets(presetableKey),
                    PresetsManager.Instance.GetDirectory(presetableKey), clickHandler, previewProvider);
        }

        private void UpdateSavedPresets() {
            if (_presetable == null) return;

            var presets = PresetsManager.Instance.GetSavedPresets(_presetable.UserPresetableKey)
                    .ToIReadOnlyListIfItsNot();

            SetValue(SavedPresetsPropertyKey, new ObservableCollection<ISavedPresetEntry>(presets));
            SetValue(SavedPresetsGroupedPropertyKey, new ObservableCollection<MenuItem>(
                    GroupPresets(presets, PresetsManager.Instance.GetDirectory(_presetable.UserPresetableKey), MenuItem_Click, _presetable as IPreviewProvider)));
            
            _ignoreNext = true;
            CurrentUserPreset = presets.FirstOrDefault(x => x.Filename == SelectedPresetFilename); // ?? presets.FirstOrDefault();
            _ignoreNext = false;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e) {
            e.Handled = true;

            var entry = (((MenuItem)sender).Tag as TagHelper)?.Entry;
            if (entry == null) return;

            if (CurrentUserPreset != entry) {
                CurrentUserPreset = entry;
            } else {
                SelectionChanged(entry);
            }
        }

        private static readonly DependencyPropertyKey ChangedPropertyKey = DependencyProperty.RegisterReadOnly("Changed", 
            typeof(bool), typeof(UserPresetsControl), null);
        public static readonly DependencyProperty ChangedProperty = ChangedPropertyKey.DependencyProperty;

        public bool Changed => (bool)GetValue(ChangedProperty);

        private void SetChanged(bool? value = null) {
            if (_presetable == null || Changed == value) return;
            
            SetValue(ChangedPropertyKey, value ?? ValuesStorage.GetBool("__userpresets_c_" + _presetable.UserPresetableKey));
            if (value.HasValue) {
                ValuesStorage.Set("__userpresets_c_" + _presetable.UserPresetableKey, value.Value);
            }
        }

        private string _currentUserPresetData;

        private void Presetable_Changed(object sender, EventArgs e) {
            if (OptionSmartChangedHandling) {
                if (_currentUserPresetData == null) {
                    _currentUserPresetData = CurrentUserPreset?.ReadData();
                }

                var actualData = _presetable.ExportToUserPresetData();
                SetChanged(actualData != _currentUserPresetData);
            } else {
                SetChanged(true);
            }
        }

        private void Presets_Update(object sender, EventArgs e) {
            UpdateSavedPresets();
        }

        public IUserPresetable UserPresetable {
            get { return (IUserPresetable)GetValue(UserPresetableProperty); }
            set { SetValue(UserPresetableProperty, value); }
        }

        public static readonly DependencyProperty ShowPreviewProperty = DependencyProperty.Register(nameof(ShowPreview), typeof(bool),
                typeof(UserPresetsControl));

        public bool ShowPreview {
            get { return (bool)GetValue(ShowPreviewProperty); }
            set { SetValue(ShowPreviewProperty, value); }
        }

        public static readonly DependencyProperty PreviewTemplateProperty = DependencyProperty.Register(nameof(PreviewTemplate), typeof(DataTemplate),
                typeof(UserPresetsControl));

        public DataTemplate PreviewTemplate {
            get { return (DataTemplate)GetValue(PreviewTemplateProperty); }
            set { SetValue(PreviewTemplateProperty, value); }
        }
    }
}
