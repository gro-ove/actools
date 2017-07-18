using FirstFloor.ModernUI.Windows.Controls;
using System;
using System.Linq;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class CombinePythonAppsPresetsDialog {
        public CombinePythonAppsPresetsDialog() {
            DataContext = new ViewModel();
            InitializeComponent();
            Buttons = new [] { CreateExtraDialogButton(UiStrings.Ok, new DelegateCommand(() => {
                if (Model.Save()) {
                    Close();
                }
            }), true), CancelButton };

            this.OnActualUnload(Model.Unload);
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class CombineDesktopEntry : Displayable {
            private bool _isSelected;

            public bool IsSelected {
                get => _isSelected;
                set {
                    if (Equals(value, _isSelected)) return;
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }

            private ISavedPresetEntry _preset;

            public ISavedPresetEntry Preset {
                get => _preset;
                set {
                    if (Equals(value, _preset)) return;
                    _preset = value;
                    OnPropertyChanged();

                    var forms = FormsSettings.Load(AcSettingsHolder.GetFormsIniFromPreset(value.ReadData()));
                    Desktops = Enumerable.Range(0, 4).Select(x => new SettingEntry(x, forms.GetVisibleForms(x))).ToArray();
                    PresetDesktopToUse = Desktops.FirstOrDefault(x => x.DisplayName != null);
                }
            }

            private SettingEntry[] _desktops;

            public SettingEntry[] Desktops {
                get => _desktops;
                set {
                    if (Equals(value, _desktops)) return;
                    _desktops = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _presetDesktopToUse;

            public SettingEntry PresetDesktopToUse {
                get => _presetDesktopToUse;
                set {
                    if (Equals(value, _presetDesktopToUse)) return;
                    _presetDesktopToUse = value;
                    OnPropertyChanged();
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();
            public HierarchicalGroup AppPresets { get; }

            public CombineDesktopEntry[] Entries { get; }

            public ViewModel() {
                AppPresets = _helper.CreateGroup(AcSettingsHolder.AppsPresetsCategory);
                Entries = Enumerable.Range(0, 4).Select(x => new CombineDesktopEntry {
                    DisplayName = $"Desktop #{x + 1}",
                    IsSelected = x == 0
                }).ToArray();
                foreach (var desktop in Entries) {
                    desktop.PropertyChanged += (sender, args) => {
                        if (args.PropertyName == nameof(CombineDesktopEntry.IsSelected) && ((CombineDesktopEntry)sender).IsSelected) {
                            foreach (var o in Entries.ApartFrom(sender)) {
                                o.IsSelected = false;
                            }
                        }
                    };
                }
            }

            public bool Save() {
                try {
                    var data = AcSettingsHolder.CombineAppPresets(Entries.Select(x => Tuple.Create(x.Preset, x.PresetDesktopToUse.IntValue ?? 0)),
                            Entries.FindIndex(x => x.IsSelected) + 1);
                    string filename = null;
                    if (PresetsManager.Instance.SavePresetUsingDialog(AcSettingsHolder.AppsPresetsKey, AcSettingsHolder.AppsPresetsCategory, data, ref filename)) {
                        UserPresetsControl.LoadPreset(AcSettingsHolder.AppsPresetsKey, filename);
                        return true;
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t combine presets", e);
                }

                return false;
            }

            public void Unload() {
                _helper.Dispose();
            }
        }
    }
}
