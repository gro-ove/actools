using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.QuickSwitches {
    public static class QuickSwitchCommands {
        static QuickSwitchCommands() {
            PresetsManager.PresetSaved += OnPresetSaved;
        }

        private static void OnPresetSaved(object sender, PresetSavedEventArgs e) {
            if (e.Key == RhmService.Instance.PresetableKey) {
                UserPresetsControl.SetCurrentFilename(RhmService.Instance.PresetableKey, e.Filename);
            }
        }

        public static ICommand GoCommand = new AsyncCommand(() => QuickDrive.RunAsync());

        public static DelegateCommand RhmMenuCommand = new DelegateCommand(() => {
            var currentPreset = UserPresetsControl.GetCurrentFilename(RhmService.Instance.PresetableKey);
            var presetsItem = new MenuItem {
                Header = new BbCodeBlock {
                    BbCode = currentPreset == null ? @"Current Preset: [i]None[/i]" : "Current Preset: " + Path.GetFileNameWithoutExtension(currentPreset)
                },
                StaysOpenOnClick = true
            };

            foreach (var menuItem in PresetsMenuHelper.GroupPresets(RhmService.Instance.PresetableCategory,
                    p => {
                        UserPresetsControl.SetCurrentFilename(RhmService.Instance.PresetableKey, p.Filename);
                        RhmService.Instance.ImportFromPresetData(p.ReadData());
                    })) {
                presetsItem.Items.Add(menuItem);
            }

            var icons = new SharedResourceDictionary {
                Source = new Uri("/AcManager.Controls;component/Assets/IconData.xaml", UriKind.Relative)
            };

            new ContextMenu()
                    .AddItem(presetsItem)
                    .AddItem("Save", new DelegateCommand(() => {
                        var presetable = RhmService.Instance;
                        try {
                            PresetsManager.Instance.SavePresetUsingDialog(presetable.PresetableKey, presetable.PresetableCategory,
                                    presetable.ExportToPresetData(), UserPresetsControl.GetCurrentFilename(presetable.PresetableKey));
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t save preset", e);
                        }
                    }), iconData: icons["SaveIconData"] as Geometry)
                    .AddItem("Share link", new AsyncCommand(async () => {
                        var presetable = RhmService.Instance;
                        try {
                            var data = presetable.ExportToPresetData();
                            if (data == null) return;
                            await SharingUiHelper.ShareAsync(SharedEntryType.RhmPreset,
                                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(presetable.PresetableKey)), null,
                                    data);
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t share preset", e);
                        }
                    }))
                    .AddSeparator()
                    .AddItem("RHM settings", RhmService.Instance.ShowSettingsCommand, iconData: icons["GearIconData"] as Geometry)
                    .IsOpen = true;
        }, () => SettingsHolder.Drive.RhmIntegration && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.RhmLocation));
    }
}