using System.Windows.Controls;
using AcManager.Pages.Settings;
using AcManager.Tools.GameProperties;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using Hardcodet.Wpf.TaskbarNotification;

namespace AcManager {
    public partial class AppHibernator {
        private TaskbarIcon _icon;

        private void AddTrayIconWpf() {
            ActionExtension.InvokeInMainThreadAsync(() => {
                var patchSettings = SettingsShadersPatch.IsCustomShadersPatchInstalled() ? new MenuItem {
                    Header = "Custom Shaders Patch settings",
                    Command = SettingsShadersPatch.GetShowSettingsCommand()
                } : null;
                if (patchSettings != null) {
                    LimitedService.SetLimited(patchSettings, true);
                }

                var rhm = RhmService.Instance.Active
                        ? new MenuItem {
                            Header = "RHM settings",
                            Command = RhmService.Instance.ShowSettingsCommand
                        }
                        : null;

                var restore = new MenuItem { Header = UiStrings.Restore };
                var close = new MenuItem { Header = UiStrings.Close };

                restore.Click += OnRestoreMenuItemClick;
                close.Click += OnCloseMenuItemClick;

                _icon = new TaskbarIcon {
                    Icon = AppIconService.GetTrayIcon(),
                    ToolTipText = AppStrings.Hibernate_TrayText,
                    ContextMenu = new ContextMenu()
                            .AddItem(patchSettings)
                            .AddItem(rhm)
                            .AddSeparator()
                            .AddItem(restore)
                            .AddItem(close),
                    DoubleClickCommand = new DelegateCommand(WakeUp)
                };
            });
        }

        private void RemoveTrayIconWpf() {
            ActionExtension.InvokeInMainThreadAsync(() => {
                DisposeHelper.Dispose(ref _icon);
            });
        }
    }
}