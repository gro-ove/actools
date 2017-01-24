using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class RhmService : NotifyPropertyChanged, IDisposable {
        public static TimeSpan OptionKeepRunning = TimeSpan.Zero;

        private static RhmService _instance;

        public static RhmService Instance => _instance ?? (_instance = new RhmService());

        private RhmService() { }

        public void SetListener() {
            SettingsHolder.Drive.PropertyChanged += OnSettingChanged;
            GameWrapper.Started += OnGameStarted;
            GameWrapper.Ended += OnGameEnded;
        }

        private bool _active;

        public bool Active {
            get { return _active; }
            set {
                if (Equals(value, _active)) return;
                _active = value;
                OnPropertyChanged();
            }
        }

        private void OnGameStarted(object sender, GameStartedArgs e) {
            if (SettingsHolder.Drive.RhmIntegration && !NonCmInstanceRunned()) {
                Active = true;
                KeepRunning();
            }
        }

        private async void KeepRunning() {
            do {
                if (!await EnsureRunnedAsync()) {
                    Logging.Error("Can’t keep RHM service running");
                    break;
                }

                if (_process == null) {
                    Logging.Unexpected();
                    break;
                }

                await _process.WaitForExitAsync();
            } while (Active);
        }

        private void OnGameEnded(object sender, GameEndedArgs e) {
            Active = false;
            EnsureStoppedLater().Forget();
        }

        private int _stoppingLaterId;

        private async Task EnsureStoppedLater() {
            var id = ++_stoppingLaterId;
            await Task.Delay(OptionKeepRunning);
            if (id == _stoppingLaterId && !Active) {
                EnsureStopped();
            }
        }

        private void OnSettingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.Drive.RhmLocation):
                case nameof(SettingsHolder.Drive.RhmIntegration):
                    _showSettingsCommand?.RaiseCanExecuteChanged();
                    EnsureStopped();
                    break;
            }
        }

        private bool CheckSettings() {
            if (!SettingsHolder.Drive.RhmIntegration) return false;

            if (string.IsNullOrWhiteSpace(SettingsHolder.Drive.RhmLocation)) {
                NonfatalError.Notify(ToolsStrings.RhmService_CannotStart, "You forgot to specify its location.");
                return false;
            }

            if (!File.Exists(SettingsHolder.Drive.RhmLocation)) {
                NonfatalError.Notify(ToolsStrings.RhmService_CannotStart, "You forgot to specify its location.");
                return false;
            }

            return true;
        }

        [CanBeNull]
        private Process _process;

        private bool SetVisibility(bool visible) {
            if (_process != null) {
                var handle = _process.GetWindowsHandles().FirstOrDefault(h => User32.GetText(h).StartsWith(@"Real Head Motion for Assetto Corsa "));
                if (handle != default(IntPtr)) {
                    User32.ShowWindow(handle, visible ? User32.WindowShowStyle.Show : User32.WindowShowStyle.Hide);
                    return true;
                }
            }

            return false;
        }

        private bool NonCmInstanceRunned() {
            var name = Path.GetFileNameWithoutExtension(SettingsHolder.Drive.RhmLocation);
            return Process.GetProcessesByName(name).Any(x => x.Id != _process?.Id);
        }

        private async Task<bool> RunRhmAsync(bool keepVisible = false) {
            if (SettingsHolder.Drive.RhmLocation == null) return false;

            try {
                _process = Process.Start(new ProcessStartInfo {
                    FileName = SettingsHolder.Drive.RhmLocation,
                    WorkingDirectory = Path.GetDirectoryName(SettingsHolder.Drive.RhmLocation) ?? ""
                });
                if (_process == null) throw new Exception(@"Process=NULL");
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.RhmService_CannotStart, e);
                return false;
            }

            ChildProcessTracker.AddProcess(_process);
            if (keepVisible) return true;

            for (var i = 0; i < 100; i++) {
                if (SetVisibility(false)) return true;
                await Task.Delay(10);
            }

            NonfatalError.Notify("Can’t find app’s window");
            EnsureStopped();
            return false;
        }

        private async Task<bool> EnsureRunnedAsync(bool keepVisible = false) {
            if (SettingsHolder.Drive.RhmLocation == null) return false;

            if (_process == null || _process.HasExitedSafe()) {
                DisposeHelper.Dispose(ref _process);
                if (!await RunRhmAsync(keepVisible)) return false;
            }

            return true;
        }

        private void EnsureStopped() {
            try {
                if (_process?.HasExitedSafe() == false) {
                    _process?.Kill();
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
            DisposeHelper.Dispose(ref _process);
        }

        private AsyncCommand _showSettingsCommand;

        public AsyncCommand ShowSettingsCommand => _showSettingsCommand ?? (_showSettingsCommand = new AsyncCommand(async () => {
            if (!CheckSettings()) return;

            if (NonCmInstanceRunned()) {
                NonfatalError.Notify("Can’t show RHM settings", "Real Head Motion already started, but not by CM.");
                return;
            }

            if (!await EnsureRunnedAsync(true)) return;
            SetVisibility(true);
        }, () => SettingsHolder.Drive.RhmIntegration && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.RhmLocation)));

        public void Dispose() {
            EnsureStopped();
        }
    }
}