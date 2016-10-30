using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using Application = System.Windows.Application;

namespace AcManager {
    public class AppHibernator : IDisposable {
        public static bool OptionDisableProcessing = false;

        public void SetListener() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;
            UpdateGameListeners();
        }

        private bool _added;

        private void UpdateGameListeners() {
            if (SettingsHolder.Drive.WatchForSharedMemory) {
                if (!_added) {
                    AcSharedMemory.Instance.Start += OnStart;
                    AcSharedMemory.Instance.Finish += OnFinish;
                    GameWrapper.Started += GameWrapper_Started;
                    GameWrapper.Ended += GameWrapper_Ended;
                    _added = true;
                }
            } else if (_added) {
                AcSharedMemory.Instance.Start -= OnStart;
                AcSharedMemory.Instance.Finish -= OnFinish;
                GameWrapper.Started -= GameWrapper_Started;
                GameWrapper.Ended -= GameWrapper_Ended;
                _added = false;
            }
        }

        private bool _raceStartedByCm;

        private void GameWrapper_Started(object sender, GameStartedArgs e) {
            _raceStartedByCm = true;
        }

        private async void GameWrapper_Ended(object sender, GameEndedArgs e) {
            await Task.Delay(5000);
            _raceStartedByCm = false;
        }

        private void Drive_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.Drive.WatchForSharedMemory)) {
                UpdateGameListeners();
            }
        }

        private IDisposable _pausedProcessing;
        private IList<Window> _hiddenWindows;
        private NotifyIcon _trayIcon;

        private void Hibernate() {
            Logging.Here();

            /* add an icon to the tray for manual restoration just in case */
            _trayIcon = new NotifyIcon {
                Icon = AppIconService.GetTrayIcon(),
                Text = AppStrings.Hibernate_TrayText,
                Visible = true
            };

            _trayIcon.DoubleClick += TrayIcon_DoubleClick;

            var restoreMenuItem = new ToolStripMenuItem { Text = UiStrings.Restore };
            restoreMenuItem.Click += RestoreMenuItem_Click;

            var closeMenuItem = new ToolStripMenuItem { Text = UiStrings.Close };
            closeMenuItem.Click += CloseMenuItem_Click;

            _trayIcon.ContextMenuStrip = new ContextMenuStrip {
                Items = {
                    restoreMenuItem,
                    closeMenuItem
                }
            };

            Application.Current.Dispatcher.Invoke(() => {
                /* hide windows */
                _hiddenWindows = Application.Current.Windows.OfType<Window>().Where(x => x.Visibility == Visibility.Visible).ToList();
                foreach (var window in _hiddenWindows) {
                    window.Visibility = Visibility.Collapsed;
                }

                /* pause processing */
                if (OptionDisableProcessing) {
                    _pausedProcessing = Application.Current.Dispatcher.DisableProcessing();
                }
            });
        }

        private void RestoreMenuItem_Click(object sender, EventArgs e) {
            Hibernated = false;
        }

        private void CloseMenuItem_Click(object sender, EventArgs e) {
            Application.Current.Shutdown();
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e) {
            Hibernated = false;
        }

        private void WakeUp() {
            Logging.Here();

            /* restore processing */
            DisposeHelper.Dispose(ref _pausedProcessing);

            /* show hidden windows */
            Application.Current.Dispatcher.Invoke(() => {
                if (_hiddenWindows != null) {
                    foreach (var window in _hiddenWindows) {
                        window.Visibility = Visibility.Visible;
                    }
                    _hiddenWindows = null;
                }

                if (_raceStartedByCm) {
                    (Application.Current.MainWindow as DpiAwareWindow)?.BringToFront();
                }
            });

            /* remove tray icon */
            if (_trayIcon != null) {
                _trayIcon.Visible = false;
                DisposeHelper.Dispose(ref _trayIcon);
            }
        }

        private bool _hibernated;

        public bool Hibernated {
            get { return _hibernated; }
            set {
                if (Equals(value, _hibernated)) return;
                _hibernated = value;

                try {
                    if (value) {
                        Hibernate();
                    } else {
                        WakeUp();
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }
        }

        private void OnStart(object sender, EventArgs e) {
            Hibernated = true;
        }

        private void OnFinish(object sender, EventArgs e) {
            Hibernated = false;
        }

        public void Dispose() {
            Hibernated = false;
        }
    }
}