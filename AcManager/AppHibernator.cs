using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Pages.Settings;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.Utils.Helpers;
using AcTools.Windows.Input;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Forms.ContextMenu;
using MenuItem = System.Windows.Forms.MenuItem;

namespace AcManager {
    public partial class AppHibernator : IDisposable {
        private IKeyboardListener _keyboard;

        private void InitKeyboardWatcher() {
            if (_keyboard != null) return;
            _keyboard = KeyboardListenerFactory.Get();
            _keyboard.WatchFor(Keys.Oemtilde);
            _keyboard.PreviewKeyDown += (sender, args) => {
                if (SettingsHolder.Drive.ShowCspSettingsWithShortcut && args.Key == Keys.Oemtilde
                        && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                    if (SettingsShadersPatch.CloseOpenedSettings != null) {
                        SettingsShadersPatch.CloseOpenedSettings();
                    } else {
                        ActionExtension.InvokeInMainThreadAsync(() => SettingsShadersPatch.GetShowSettingsCommand().Execute(null));
                    }
                }
            };
        }

        public void SetListener() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;
            UpdateGameListeners();

            if (SettingsHolder.Drive.ShowCspSettingsWithShortcut) {
                InitKeyboardWatcher();
            } else {
                SettingsHolder.Drive.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(SettingsHolder.Drive.ShowCspSettingsWithShortcut)) {
                        InitKeyboardWatcher();
                    }
                };
            }

#if DEBUG
            Hibernated = true;
#endif
        }

        private bool _added;

        private void UpdateGameListeners() {
            if (SettingsHolder.Drive.WatchForSharedMemory) {
                if (!_added) {
                    AcSharedMemory.Instance.Start += OnStart;
                    AcSharedMemory.Instance.Finish += OnFinish;
                    AcSharedMemory.Instance.MonitorFramesPerSecondBegin += OnStart;
                    AcSharedMemory.Instance.MonitorFramesPerSecondEnd += OnFinish;
                    GameWrapper.Started += OnGameWrapperStarted;
                    GameWrapper.Ended += OnGameWrapperEnded;
                    _added = true;
                }
            } else if (_added) {
                AcSharedMemory.Instance.Start -= OnStart;
                AcSharedMemory.Instance.Finish -= OnFinish;
                AcSharedMemory.Instance.MonitorFramesPerSecondBegin -= OnStart;
                AcSharedMemory.Instance.MonitorFramesPerSecondEnd -= OnFinish;
                GameWrapper.Started -= OnGameWrapperStarted;
                GameWrapper.Ended -= OnGameWrapperEnded;
                _added = false;
            }
        }

        private bool _raceStartedByCm;

        private void OnGameWrapperStarted(object sender, GameStartedArgs e) {
            _raceStartedByCm = true;
        }

        private async void OnGameWrapperEnded(object sender, GameEndedArgs e) {
            await Task.Delay(5000);
            _raceStartedByCm = false;
        }

        private void Drive_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.Drive.WatchForSharedMemory)) {
                UpdateGameListeners();
            }
        }

        private IList<Window> _hiddenWindows;
        private NotifyIcon _trayIcon;

        private void AddTrayIcon() {
            _trayIcon = new NotifyIcon {
                Icon = AppIconService.GetTrayIcon(),
                Text = AppStrings.Hibernate_TrayText
            };

            _trayIcon.DoubleClick += OnTrayIconDoubleClick;

            var patchSettings = SettingsShadersPatch.IsCustomShadersPatchInstalled() ? new MenuItem { Text = "Custom Shaders Patch settings" } : null;
            if (patchSettings != null) {
                patchSettings.Click += (sender, args) => SettingsShadersPatch.GetShowSettingsCommand().Execute(null);
            }

            var rhm = RhmService.Instance.Active ? new MenuItem { Text = "RHM settings" } : null;
            if (rhm != null) {
                rhm.Click += (sender, args) => RhmService.Instance.ShowSettingsCommand.ExecuteAsync().Ignore();
            }

            var restoreMenuItem = new MenuItem { Text = UiStrings.Restore };
            restoreMenuItem.Click += OnRestoreMenuItemClick;

            var closeMenuItem = new MenuItem { Text = UiStrings.Close };
            closeMenuItem.Click += OnCloseMenuItemClick;

            _trayIcon.ContextMenu = new ContextMenu(new[] {
                patchSettings,
                rhm,
                new MenuItem(@"-"),
                restoreMenuItem,
                closeMenuItem
            }.NonNull().ToArray());

            _trayIcon.Visible = true;
        }

        private void RemoveTrayIcon() {
            if (_trayIcon != null) {
                _trayIcon.Visible = false;
                DisposeHelper.Dispose(ref _trayIcon);
            }
        }

        private void OnRestoreMenuItemClick(object sender, EventArgs e) {
            WakeUp();
        }

        private void OnCloseMenuItemClick(object sender, EventArgs e) {
            var app = Application.Current;
            if (app == null) {
                Environment.Exit(0);
            } else {
                app.Shutdown();
            }
        }

        private void OnTrayIconDoubleClick(object sender, EventArgs e) {
            Hibernated = false;
        }

        private void WakeUp() {
            _raceStartedByCm = true;
            Hibernated = false;
        }

        private bool _hibernated;

        public bool Hibernated {
            get => _hibernated;
            set {
                if (Equals(value, _hibernated)) return;
                _hibernated = value;

                ActionExtension.InvokeInMainThreadAsync(() => {
                    try {
                        if (value) {
                            /* add an icon to the tray for manual restoration just in case */
                            AddTrayIcon();
                            // AddTrayIconWpf();

                            /* hide windows */
                            _hiddenWindows = Application.Current?.Windows.OfType<Window>().Where(x => x.Visibility == Visibility.Visible
                                    && !x.Topmost).ToList();
                            if (_hiddenWindows != null) {
                                foreach (var window in _hiddenWindows) {
                                    window.Visibility = Visibility.Collapsed;
                                }
                            }
                        } else {
                            /* show hidden windows */
                            if (_hiddenWindows != null) {
                                foreach (var window in _hiddenWindows) {
                                    window.Visibility = Visibility.Visible;
                                }
                                _hiddenWindows = null;
                            }

                            if (_raceStartedByCm) {
                                (Application.Current?.MainWindow as DpiAwareWindow)?.BringToFront();
                            }

                            /* remove tray icon */
                            RemoveTrayIcon();
                            RemoveTrayIconWpf();
                        }
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                });
            }
        }

        private void OnStart(object sender, EventArgs e) {
            if (!SettingsHolder.Drive.HideWhileRacing) return;
            Hibernated = true;
        }

        private void OnFinish(object sender, EventArgs e) {
            Hibernated = false;
        }

        public void Dispose() {
            RemoveTrayIcon();
            RemoveTrayIconWpf();
        }
    }
}