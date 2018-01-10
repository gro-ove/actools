using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AcManager.Tools.SharedMemory;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Starters {
    public class DeveloperStarter : IAcsStarter {
        public bool RunSteamIfNeeded { get; set; }

        private string _resultFilename;
        private FileSystemWatcher _watcher;
        private DispatcherTimer _timer;

        public void Run() {
            if (_watcher != null) return;

            _resultFilename = AcPaths.GetResultJsonFilename();
            FileUtils.EnsureFileDirectoryExists(_resultFilename);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5), IsEnabled = true };
            _timer.Tick += OnTick;

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_resultFilename) ?? "") {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += FileUpdate;
            _watcher.Created += FileUpdate;
        }

        private void OnTick(object sender, EventArgs eventArgs) {
            var process = AcProcess.TryToFind();
            if (process != null) {
                _timer.IsEnabled = false;
                _process = process;
                _processTcs?.TrySetResult(process);
            }
        }

        private TaskCompletionSource<bool> _tcs;
        private TaskCompletionSource<Process> _processTcs;
        private Process _process;
        private readonly Busy _busy = new Busy();

        private void FileUpdate(object sender, FileSystemEventArgs e) {
            if (FileUtils.ArePathsEqual(e.FullPath, _resultFilename)) {
                _timer.IsEnabled = false;
                _processTcs?.TrySetResult(null);
                _busy.DoDelay(() => {
                    _tcs?.TrySetResult(true);
                }, 500);
            }
        }

        public Task RunAsync(CancellationToken cancellation) {
            Run();
            return Task.Delay(0);
        }

        public void WaitUntilGame() {}

        public Task<Process> WaitUntilGameAsync(CancellationToken cancellation) {
            _processTcs = new TaskCompletionSource<Process>();
            cancellation.Register(() => _processTcs?.TrySetCanceled());
            return _processTcs.Task;
        }

        public void WaitGame() {
            WaitGameAsync(default(CancellationToken)).Wait();
        }

        public Task WaitGameAsync(CancellationToken cancellation) {
            _tcs = new TaskCompletionSource<bool>();
            cancellation.Register(() => {
                if (!_process.HasExitedSafe()) {
                    try {
                        _process.Kill();
                    } catch (Exception e) {
                        AcToolsLogging.Write(e);
                    }
                }

                _tcs?.TrySetCanceled();
            });

            return _tcs.Task;
        }

        public void CleanUp() {
            _watcher?.Dispose();
        }

        public Task CleanUpAsync(CancellationToken cancellation) {
            _watcher?.Dispose();
            return Task.Delay(0);
        }
    }
}