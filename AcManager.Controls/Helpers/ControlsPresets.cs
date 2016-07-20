using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public class ControlsPresets: NotifyPropertyChanged, IPreviewProvider {
        private static ControlsPresets _instance;

        public static ControlsPresets Instance => _instance ?? (_instance = new ControlsPresets());

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

        private AcSettingsHolder.ControlsSettings Controls => AcSettingsHolder.Controls;

        private async Task<MenuItem> RebuildAsync(string header, [Localizable(false)] string sub) {
            var result = new MenuItem { Header = header };
            var directory = Path.Combine(Controls.PresetsDirectory, sub);
            var list = await Task.Run(() => FileUtils.GetFiles(directory, @"*.ini").Select(x => new PresetEntry(x)).ToList());
            foreach (var item in UserPresetsControl.GroupPresets(list, directory, ClickHandler, this, @".ini")) {
                result.Items.Add(item);
            }
            return result;
        }

        private void ClickHandler(object sender, RoutedEventArgs e) {
            if (!PresetsReady) return;
            e.Handled = true;

            var entry = (((MenuItem)sender).Tag as UserPresetsControl.TagHelper)?.Entry as PresetEntry;
            if (entry == null || (Controls.CurrentPresetName == null || Controls.CurrentPresetChanged) &&
                    ModernDialog.ShowMessage(
                            string.Format(Resources.Controls_LoadPresetWarning, entry.DisplayName),
                            Resources.Common_AreYouSure, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                return;
            }
            
            Controls.LoadPreset(entry.Filename);
        }

        private async Task RebuildPresetsList() {
            if (_reloading) return;

            _reloading = true;
            PresetsReady = false;

            try {
                Presets.Clear();
                Presets.Add(await RebuildAsync(Resources.Controls_BuiltInPresets, "presets"));
                Presets.Add(await RebuildAsync(Resources.Controls_UserPresets, "savedsetups"));
                PresetsReady = true;
            } catch (Exception e) {
                Logging.Warning("RebuildPresetsList() exception: " + e);
            } finally {
                _reloading = false;
            }
        }

        object IPreviewProvider.GetPreview(string serializedData) {
            var ini = IniFile.Parse(serializedData);
            var result = new StringBuilder();

            // input method
            result.AppendFormat(Resources.Controls_Preview_InputMethod, ini["HEADER"].GetEntry("INPUT_METHOD", Controls.InputMethods).DisplayName);

            // device
            var section = ini["CONTROLLERS"];
            var devices = LinqExtension.RangeFrom().Select(x => section.Get("CON" + x.ToInvariantString())).TakeWhile(x => x != null).Distinct().ToList();
            if (devices.Count > 1) {
                result.Append('\n');
                result.AppendFormat(Resources.Controls_Preview_Devices, devices.JoinToString(@", "));
            } else if (devices.Count == 1) {
                result.Append('\n');
                result.AppendFormat(Resources.Controls_Preview_Device, devices[0]);
            }

            return new BbCodeBlock {
                BbCode = result.ToString()
            };
        }

        public BetterObservableCollection<MenuItem> Presets { get; } = new BetterObservableCollection<MenuItem>();
    }
}