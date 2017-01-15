using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;

namespace AcManager.Tools.Data {
    internal static class KunosLauncherDataWatcher {
        private static FileSystemWatcher _fsWatcher;
        private static int _subscribers;

        private static void EnsureInitialized() {
            if (_fsWatcher == null) {
                var directory = Path.Combine(FileUtils.GetDocumentsDirectory(), @"launcherdata", @"filestore");
                Directory.CreateDirectory(directory);
                _fsWatcher = new FileSystemWatcher {
                    Path = directory,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
            }
        }

        public static IDisposable Subscribe(string filename, Action reload, Func<bool> ignoreChanges = null) {
            EnsureInitialized();
            _subscribers++;

            var inProgress = false;

            Func<Task> reloadLater = async () => {
                if (inProgress) return;

                try {
                    inProgress = true;

                    await Task.Delay(200);
                    if (ignoreChanges?.Invoke() != true) {
                        (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(reload);
                    }
                } finally {
                    inProgress = false;
                }
            };

            FileSystemEventHandler handler = (sender, e) => {
                if (FileUtils.IsAffected(e.FullPath, filename) && ignoreChanges?.Invoke() != true) {
                    reloadLater?.Invoke();
                }
            };

            RenamedEventHandler renamedHandler = (sender, e) => {
                if ((FileUtils.IsAffected(e.FullPath, filename) || FileUtils.IsAffected(e.OldFullPath, filename)) && ignoreChanges?.Invoke() != true) {
                    reloadLater?.Invoke();
                }
            };

            _fsWatcher.Changed += handler;
            _fsWatcher.Created += handler;
            _fsWatcher.Deleted += handler;
            _fsWatcher.Renamed += renamedHandler;

            return new ActionAsDisposable(() => {
                if (handler == null) return;

                _fsWatcher.Changed -= handler;
                _fsWatcher.Created -= handler;
                _fsWatcher.Deleted -= handler;
                _fsWatcher.Renamed -= renamedHandler;

                if (--_subscribers == 0 && _fsWatcher != null) {
                    _fsWatcher.EnableRaisingEvents = false;
                    _fsWatcher = null;
                    reloadLater = null;
                    reload = null;
                    handler = null;
                    renamedHandler = null;
                }
            });
        }
    }
}