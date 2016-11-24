using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.GameProperties {
    public class RhmService : NotifyPropertyChanged, IDisposable {

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
                EnsureRunned();
                await _process.WaitForExitAsync();
            } while (Active);
        }

        private void OnGameEnded(object sender, GameEndedArgs e) {
            Active = false;
            EnsureStopped();
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
                NonfatalError.Notify("Can’t start Real Head Motion application", "You forgot to specify its location.");
                return false;
            }

            if (!File.Exists(SettingsHolder.Drive.RhmLocation)) {
                NonfatalError.Notify("Can’t start Real Head Motion application", "You forgot to specify its location.");
                return false;
            }

            return true;
        }

        private Process _process;

        private bool SetVisibility(bool visible) {
            var handle = _process.GetWindowsHandles().FirstOrDefault(h => User32.GetText(h).StartsWith("Real Head Motion for Assetto Corsa "));
            if (handle != default(IntPtr)) {
                User32.ShowWindow(handle, visible ? User32.WindowShowStyle.Show : User32.WindowShowStyle.Hide);
                return true;
            }

            return false;
        }

        private bool NonCmInstanceRunned() {
            var name = Path.GetFileNameWithoutExtension(SettingsHolder.Drive.RhmLocation);
            return Process.GetProcessesByName(name).Any(x => x.Id != _process?.Id);
        }

        private bool RunRhm(bool keepVisible = false) {
            if (SettingsHolder.Drive.RhmLocation == null) return false;

            try {
                _process = Process.Start(new ProcessStartInfo {
                    FileName = SettingsHolder.Drive.RhmLocation,
                    WorkingDirectory = Path.GetDirectoryName(SettingsHolder.Drive.RhmLocation) ?? ""
                });
                if (_process == null) throw new Exception(@"Process=NULL");
            } catch (Exception e) {
                NonfatalError.Notify("Can’t start RHM", e);
                return false;
            }

            ChildProcessTracker.AddProcess(_process);
            if (keepVisible) return true;

            for (var i = 0; i < 1000; i++) {
                if (SetVisibility(false)) return true;
                Thread.Sleep(1);
            }

            NonfatalError.Notify("Can’t find app’s window");
            EnsureStopped();
            return false;
        }

        private bool EnsureRunned(bool keepVisible = false) {
            if (SettingsHolder.Drive.RhmLocation == null) return false;

            if (_process == null || _process.HasExitedSafe()) {
                DisposeHelper.Dispose(ref _process);
                if (!RunRhm(keepVisible)) return false;
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

        private DelegateCommand _showSettingsCommand;

        public DelegateCommand ShowSettingsCommand => _showSettingsCommand ?? (_showSettingsCommand = new DelegateCommand(() => {
            Logging.Here();

            if (!CheckSettings()) return;

            if (NonCmInstanceRunned()) {
                NonfatalError.Notify("Can’t show RHM settings", "Real Head Motion already started, but not by CM.");
                return;
            }

            if (!EnsureRunned(true)) return;
            SetVisibility(true);
        }, () => SettingsHolder.Drive.RhmIntegration && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.RhmLocation)));

        public void Dispose() {
            EnsureStopped();
        }
    }
}