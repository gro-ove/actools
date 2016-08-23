using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public class ControlsPresets: NotifyPropertyChanged, IPresetsPreviewProvider {
        private static ControlsPresets _instance;

        public static ControlsPresets Instance => _instance ?? (_instance = new ControlsPresets());

        private const string KeyWarnIfChanged = "cp.warnifchanged";

        private bool? _warnIfChanged;

        public bool WarnIfChanged {
            get { return (bool)(_warnIfChanged ?? (_warnIfChanged = ValuesStorage.GetBool(KeyWarnIfChanged, true))); }
            set {
                if (Equals(value, WarnIfChanged)) return;
                _warnIfChanged = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyWarnIfChanged, true);
            }
        }

        public class PresetEntry : NotifyPropertyChanged, ISavedPresetEntry {
            public PresetEntry(string filename) {
                DisplayName = Path.GetFileNameWithoutExtension(filename);
                Filename = filename;
            }

            public string DisplayName { get; }

            public string Filename { get; }

            public string ReadData() {
                return FileUtils.ReadAllText(Filename);
            }
        }

        private ControlsPresets() {
            Controls.PresetsUpdated += OnPresetsUpdated;
            RebuildPresetsList().Forget();
        }

        private bool _innerReloading;

        private bool _presetsReady;

        public bool PresetsReady {
            get { return _presetsReady; }
            set {
                if (Equals(value, _presetsReady)) return;
                _presetsReady = value;
                OnPropertyChanged();
            }
        }

        private bool _reloading;

        private async void OnPresetsUpdated(object sender, EventArgs args) {
            if (_innerReloading) return;

            _innerReloading = true;

            try {
                await Task.Delay(200);
                Application.Current.Dispatcher.Invoke(() => {
                    RebuildPresetsList().Forget();
                });
            } catch (Exception e) {
                Logging.Warning("OnPresetsUpdated() exception: " + e);
            } finally {
                _innerReloading = false;
            }
        }

        private ControlsSettings Controls => AcSettingsHolder.Controls;

        private Task<List<PresetEntry>> ScanAsync([Localizable(false)] string sub) {
            var directory = Path.Combine(Controls.PresetsDirectory, sub);
            return Task.Run(() => FileUtils.GetFiles(directory, @"*" + ControlsSettings.PresetExtension).Select(x => new PresetEntry(x)).ToList());
        }

        private MenuItem Rebuild(string header, [Localizable(false)] string sub, IEnumerable<PresetEntry> presets) {
            var result = new MenuItem { Header = header };
            var directory = Path.Combine(Controls.PresetsDirectory, sub);
            foreach (var item in UserPresetsControl.GroupPresets(presets, directory, ClickHandler, this, ControlsSettings.PresetExtension)) {
                result.Items.Add(item);
            }
            return result;
        }

        private void ClickHandler(object sender, RoutedEventArgs e) {
            if (!PresetsReady) return;
            e.Handled = true;

            var entry = (((MenuItem)sender).Tag as UserPresetsControl.TagHelper)?.Entry as PresetEntry;
            if (entry == null) return;
            SwitchToPreset(entry);
        }

        public void SwitchToPreset(PresetEntry entry) {
            var backup = Controls.CurrentPresetName == null || Controls.CurrentPresetChanged;
            if (backup && WarnIfChanged && ModernDialog.ShowMessage(
                    string.Format(ControlsStrings.Controls_LoadPresetWarning, entry.DisplayName),
                    ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                return;
            }

            Controls.LoadPreset(entry.Filename, backup);
        }

        public void SwitchToNext() {
            var presets = _userPresets;
            if (presets.Count < 2) return;

            var current = Controls.CurrentPresetFilename;
            var selectedId = presets.FindIndex(x => x.Filename == current);
            if (selectedId == -1 || ++selectedId >= presets.Count) {
                SwitchToPreset(presets.FirstOrDefault());
            } else {
                SwitchToPreset(presets.ElementAtOrDefault(selectedId));
            }
        }

        public void SwitchToPrevious() {
            var presets = _userPresets;
            if (presets.Count < 2) return;

            var current = Controls.CurrentPresetFilename;
            var selectedId = presets.FindIndex(x => x.Filename == current);
            if (selectedId == -1) {
                SwitchToPreset(presets.FirstOrDefault());
            } else if (--selectedId < 0) {
                SwitchToPreset(presets.LastOrDefault());
            } else {
                SwitchToPreset(presets.ElementAtOrDefault(selectedId));
            }
        }

        private List<PresetEntry> _builtInPresets, _userPresets;

        private async Task RebuildPresetsList() {
            if (_reloading) return;

            _reloading = true;
            PresetsReady = false;

            try {
                Presets.Clear();

                _builtInPresets = await ScanAsync(ControlsSettings.SubBuiltInPresets);
                _userPresets = await ScanAsync(ControlsSettings.SubUserPresets);

                Presets.Add(Rebuild(ControlsStrings.Controls_BuiltInPresets, ControlsSettings.SubBuiltInPresets, _builtInPresets));
                Presets.Add(Rebuild(ControlsStrings.Controls_UserPresets, ControlsSettings.SubUserPresets, _userPresets));
                PresetsReady = true;
            } catch (Exception e) {
                Logging.Warning("[ControlsPresets] RebuildPresetsList(): " + e);
            } finally {
                _reloading = false;
            }
        }

        object IPresetsPreviewProvider.GetPreview(string serializedData) {
            var ini = IniFile.Parse(serializedData);
            var result = new StringBuilder();

            // input method
            result.AppendFormat(ControlsStrings.Controls_Preview_InputMethod, ini["HEADER"].GetEntry("INPUT_METHOD", Controls.InputMethods).DisplayName);

            // device
            var section = ini["CONTROLLERS"];
            var devices = LinqExtension.RangeFrom().Select(x => section.GetNonEmpty("CON" + x.ToInvariantString())).TakeWhile(x => x != null).Distinct().ToList();
            if (devices.Count > 1) {
                result.Append('\n');
                result.AppendFormat(ControlsStrings.Controls_Preview_Devices, devices.JoinToString(@", "));
            } else if (devices.Count == 1) {
                result.Append('\n');
                result.AppendFormat(ControlsStrings.Controls_Preview_Device, devices[0]);
            }

            return new BbCodeBlock {
                BbCode = result.ToString()
            };
        }

        public BetterObservableCollection<MenuItem> Presets { get; } = new BetterObservableCollection<MenuItem>();
    }
}