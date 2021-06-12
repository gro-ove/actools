using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class RhmService : NotifyPropertyChanged, IDisposable, IUserPresetable {
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
            get => _active;
            set => Apply(value, ref _active);
        }

        private void OnGameStarted(object sender, GameStartedArgs e) {
            if (SettingsHolder.Drive.RhmIntegration && !NonCmInstanceRunned() && e.Mode == GameMode.Race) {
                Active = true;
                KeepRunning();
            }
        }

        private async void KeepRunning() {
            try {
                _keepAliveCancellation?.Cancel();

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
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void OnGameEnded(object sender, GameEndedArgs e) {
            Active = false;
            EnsureStoppedLater();
        }

        private int _stoppingLaterId;
        private Task _keepAliveTask;
        private CancellationTokenSource _keepAliveCancellation;
        private TimeSpan _keptAlive;

        private async Task KeepAlive(TimeSpan timeout) {
            try {
                var id = ++_stoppingLaterId;

                if (timeout < TimeSpan.Zero) {
                    EnsureStopped();
                    return;
                }

                using (var cancellation = new CancellationTokenSource()) {
                    try {
                        _keepAliveCancellation = cancellation;
                        var token = _keepAliveCancellation.Token;

                        var s = Stopwatch.StartNew();
                        Logging.Debug("RHM will be killed after: " + timeout);

                        try {
                            await Task.Delay(timeout, token);
                        } catch (Exception e) when (e.IsCancelled()) { }

                        _keptAlive += s.Elapsed;

                        if (!token.IsCancellationRequested && id == _stoppingLaterId && !Active) {
                            EnsureStopped();
                        }
                    } finally {
                        _keepAliveCancellation = null;
                        _keepAliveTask = null;
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private async Task ResetKeepAlive() {
            try {
                if (_keepAliveCancellation != null) {
                    _keepAliveCancellation.Cancel();
                    if (_keepAliveTask != null) {
                        await _keepAliveTask;
                    }

                    _keepAliveTask = KeepAlive(SettingsHolder.Drive.RhmKeepAlivePeriod.TimeSpan - _keptAlive);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void EnsureStoppedLater() {
            try {
                _keptAlive = TimeSpan.Zero;
                _keepAliveTask = KeepAlive(SettingsHolder.Drive.RhmKeepAlivePeriod.TimeSpan);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void OnSettingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.Drive.RhmLocation):
                case nameof(SettingsHolder.Drive.RhmIntegration):
                    _showSettingsCommand?.RaiseCanExecuteChanged();
                    EnsureStopped();
                    break;
                case nameof(SettingsHolder.Drive.RhmKeepAlivePeriod):
                    ResetKeepAlive().Ignore();
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

        private bool? CheckVisibility() {
            if (_process != null) {
                var handle = _process.GetWindowsHandles().FirstOrDefault(h => User32.GetText(h).StartsWith(@"Real Head Motion for Assetto Corsa "));
                if (handle != default) {
                    return (User32.GetWindowLong(handle, User32.GwlStyle) & (int)User32.WindowShowStyle.Hide) == 0;
                }
            }

            return null;
        }

        private bool SetVisibility(bool visible) {
            if (_process != null) {
                var handle = _process.GetWindowsHandles().FirstOrDefault(h => User32.GetText(h).StartsWith(@"Real Head Motion for Assetto Corsa "));
                if (handle != default) {
                    User32.ShowWindow(handle, visible ? User32.WindowShowStyle.Show : User32.WindowShowStyle.Hide);
                    return true;
                }
            }

            return false;
        }

        private bool NonCmInstanceRunned() {
            try {
                var name = Path.GetFileNameWithoutExtension(SettingsHolder.Drive.RhmLocation);
                return Process.GetProcessesByName(name).Any(x => x.Id != _process?.Id);
            } catch (Exception e) {
                Logging.Warning(e);
                return false;
            }
        }

        private async Task<bool> RunRhmAsync(bool keepVisible = false) {
            try {
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
                    await Task.Delay(50);
                }

                NonfatalError.Notify("Can’t find app’s window");
                EnsureStopped();
                return false;
            } catch (Exception e) {
                Logging.Warning(e);
                return false;
            }
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

        private void TryToRestart() {
            if (_process?.HasExitedSafe() != false) return;
            var visiblity = CheckVisibility();
            EnsureStopped();
            EnsureRunnedAsync(visiblity == true).Ignore();
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

        #region Presets
        public bool CanBeSaved {
            get {
                if (!string.IsNullOrWhiteSpace(SettingsHolder.Drive.RhmSettingsLocation)) {
                    return false;
                }

                try {
                    new FileInfo(SettingsHolder.Drive.RhmSettingsLocation).Refresh();
                    return true;
                } catch (Exception) {
                    return false;
                }
            }
        }

        public string PresetableKey => "rhmPresets";
        public PresetsCategory PresetableCategory => new PresetsCategory("Real Head Motion Presets", ".xml");

        event EventHandler IUserPresetable.Changed {
            add { }
            remove { }
        }

        public void ImportFromPresetData(string data) {
            var filename = SettingsHolder.Drive.RhmSettingsLocation;
            FileUtils.EnsureFileDirectoryExists(filename);
            File.WriteAllText(filename, data);
            TryToRestart();
        }

        public string ExportToPresetData() {
            var filename = SettingsHolder.Drive.RhmSettingsLocation;
            if (File.Exists(filename)) {
                return File.ReadAllText(filename);
            }

            throw new InformativeException("RHM settings file is missing", "Try to run RHM first or load some preset.");
        }
        #endregion
    }
}