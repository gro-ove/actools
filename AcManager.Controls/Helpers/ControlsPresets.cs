using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public class ControlsPresets : NotifyPropertyChanged, IPresetsPreviewProvider, IHierarchicalItemPreviewProvider {
        private static ControlsPresets _instance;

        public static ControlsPresets Instance => _instance ?? (_instance = new ControlsPresets());

        private const string KeyWarnIfChanged = "cp.warnifchanged";

        private bool? _warnIfChanged;

        public bool WarnIfChanged {
            get => (bool)(_warnIfChanged ?? (_warnIfChanged = ValuesStorage.Get(KeyWarnIfChanged, true)));
            set {
                if (Equals(value, WarnIfChanged)) return;
                _warnIfChanged = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyWarnIfChanged, value);
            }
        }

        public class PresetEntry : NotifyPropertyChanged, ISavedPresetEntry {
            public PresetEntry(string filename) {
                DisplayName = Path.GetFileNameWithoutExtension(filename) ?? @"?";
                VirtualFilename = filename;
            }

            public string DisplayName { get; }
            public string VirtualFilename { get; }
            public bool IsBuiltIn => false;

            public byte[] ReadBinaryData() {
                return File.ReadAllBytes(VirtualFilename);
            }

            public void SetParent(string baseDirectory) {
                // TODO
            }

            public bool Equals(ISavedPresetEntry other) {
                return other != null && string.Equals(VirtualFilename, other.VirtualFilename, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object other) {
                return Equals(other as ISavedPresetEntry);
            }

            protected bool Equals(PresetEntry other) {
                return other != null && string.Equals(VirtualFilename, other.VirtualFilename, StringComparison.OrdinalIgnoreCase);
            }

            public override int GetHashCode() {
                return VirtualFilename.GetHashCode();
            }
        }

        private ControlsPresets() {
            Controls.PresetsUpdated += OnPresetsUpdated;
            RebuildPresetsList().Forget();
        }

        private bool _innerReloading;

        private bool _presetsReady;

        public bool PresetsReady {
            get => _presetsReady;
            set => Apply(value, ref _presetsReady);
        }

        private bool _reloading;

        private async void OnPresetsUpdated(object sender, EventArgs args) {
            if (_innerReloading) return;

            _innerReloading = true;

            try {
                await Task.Delay(200);
                ActionExtension.InvokeInMainThread(() => { RebuildPresetsList().Forget(); });
            } catch (Exception e) {
                Logging.Warning("OnPresetsUpdated() exception: " + e);
            } finally {
                _innerReloading = false;
            }
        }

        private ControlsSettings Controls => AcSettingsHolder.Controls;

        private Task<List<PresetEntry>> ScanAsync([Localizable(false)] string sub) {
            var directory = Path.Combine(ControlsSettings.PresetsDirectory, sub);
            return Task.Run(() => FileUtils.GetFilesRecursive(directory, @"*" + ControlsSettings.PresetExtension).Select(x => new PresetEntry(x)).ToList());
        }

        private HierarchicalGroup Rebuild(string header, [Localizable(false)] string sub, IEnumerable<PresetEntry> presets) {
            var directory = Path.Combine(ControlsSettings.PresetsDirectory, sub);
            return new HierarchicalGroup(header, UserPresetsControl.GroupPresets(presets, directory, null, null));
        }

        public void SwitchToPreset(ISavedPresetEntry entry) {
            var backup = Controls.CurrentPresetName == null || Controls.CurrentPresetChanged;
            if (backup && WarnIfChanged && MessageDialog.Show(
                    string.Format(ControlsStrings.Controls_LoadPresetWarning, entry.DisplayName),
                    ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo, new MessageDialog.ShowMessageCallbacks(
                            () => WarnIfChanged ? (MessageBoxResult?)null : MessageBoxResult.Yes,
                            r => {
                                if (r == MessageBoxResult.Yes) {
                                    WarnIfChanged = false;
                                }
                            })) != MessageBoxResult.Yes) {
                return;
            }

            Controls.LoadPreset(entry.VirtualFilename, backup);
        }

        public void SwitchToNext() {
            var presets = _userPresets;
            if (presets.Count < 2) return;

            var current = Controls.CurrentPresetFilename;
            var selectedId = presets.FindIndex(x => x.VirtualFilename == current);
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
            var selectedId = presets.FindIndex(x => x.VirtualFilename == current);
            if (selectedId == -1) {
                SwitchToPreset(presets.FirstOrDefault());
            } else if (--selectedId < 0) {
                SwitchToPreset(presets.LastOrDefault());
            } else {
                SwitchToPreset(presets.ElementAtOrDefault(selectedId));
            }
        }

        private List<PresetEntry> _builtInPresets, _userPresets;

        // TODO: Split for optimization?
        private async Task RebuildPresetsList() {
            if (_reloading) return;

            _reloading = true;
            PresetsReady = false;

            try {
                Presets.Clear();

                _builtInPresets = await ScanAsync(ControlsSettings.SubBuiltInPresets);
                _userPresets = await ScanAsync(ControlsSettings.SubUserPresets);

                Presets.Add(Rebuild(ControlsStrings.Controls_BuiltInPresets, ControlsSettings.SubBuiltInPresets, _builtInPresets));
                var userPresets = Rebuild(ControlsStrings.Controls_UserPresets, ControlsSettings.SubUserPresets, _userPresets);
                if (userPresets.Count < 20) {
                    Presets.Add(new Separator());
                    foreach (var preset in userPresets) {
                        Presets.Add(preset);
                    }
                } else {
                    Presets.Add(userPresets);
                }

                PresetsReady = true;
            } catch (Exception e) {
                Logging.Warning(e);
            } finally {
                _reloading = false;
            }
        }

        public object GetPreview(string serializedData) {
            var ini = IniFile.Parse(serializedData);
            var result = new StringBuilder();

            // input method
            result.AppendFormat(ControlsStrings.Controls_Preview_InputMethod, ini["HEADER"].GetEntry("INPUT_METHOD", Controls.InputMethods).DisplayName);

            // device
            var section = ini["CONTROLLERS"];
            var devices =
                    LinqExtension.RangeFrom().Select(x => section.GetNonEmpty("CON" + x.ToInvariantString())).TakeWhile(x => x != null).Distinct().ToList();
            if (devices.Count > 1) {
                result.Append('\n');
                result.AppendFormat(ControlsStrings.Controls_Preview_Devices, devices.JoinToString(@", "));
            } else if (devices.Count == 1) {
                result.Append('\n');
                result.AppendFormat(ControlsStrings.Controls_Preview_Device, devices[0]);
            }

            return new BbCodeBlock {
                Text = result.ToString()
            };
        }

        object IHierarchicalItemPreviewProvider.GetPreview(object item) {
            var data = (item as ISavedPresetEntry)?.ReadData();
            return data == null ? null : GetPreview(data);
        }

        PlacementMode IHierarchicalItemPreviewProvider.GetPlacementMode(object item) {
            return PlacementMode.Left;
        }

        public HierarchicalGroup Presets { get; } = new HierarchicalGroup();

        public object SelectedPreset {
            get => null;
            set {
                var entry = value as ISavedPresetEntry;
                if (entry != null) {
                    SwitchToPreset(entry);
                }
            }
        }
    }
}