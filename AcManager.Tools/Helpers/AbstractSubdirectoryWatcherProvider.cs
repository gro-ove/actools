using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace AcManager.Tools.Helpers {
    public abstract class AbstractSubdirectoryWatcherProvider : IDisposable {
        public class ContentWatcher : IDisposable {
            private readonly string _path;

            public ContentWatcher(string path) {
                _path = path;
            }

            private FileSystemWatcher _fsWatcher;
            private event EventHandler UpdateInternal;

            public event EventHandler Update {
                add {
                    if (UpdateInternal == null){
                        SetFsWatcher();
                    }
                    UpdateInternal += value;
                }
                remove {
                    UpdateInternal -= value;
                    if (UpdateInternal == null){
                        UnsetFsWatcher();
                    }
                }
            }

            private void SetFsWatcher() {
                if (_fsWatcher != null) UnsetFsWatcher();

                _fsWatcher = new FileSystemWatcher {
                    Path = _path,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = "*.*"
                };
                _fsWatcher.Changed += FsWatcher_Changed;
                _fsWatcher.Created += FsWatcher_Changed;
                _fsWatcher.Deleted += FsWatcher_Changed;
                _fsWatcher.Renamed += FsWatcher_Changed;
                _fsWatcher.EnableRaisingEvents = true;
            }

            private void UnsetFsWatcher() {
                if (_fsWatcher == null) return;
                _fsWatcher.EnableRaisingEvents = false;
                _fsWatcher.Changed -= FsWatcher_Changed;
                _fsWatcher.Created -= FsWatcher_Changed;
                _fsWatcher.Deleted -= FsWatcher_Changed;
                _fsWatcher.Renamed -= FsWatcher_Changed;
                _fsWatcher.Dispose();
                _fsWatcher = null;
            }

            private void FsWatcher_Changed(object sender, FileSystemEventArgs e) {
                Dispatch();
            }

            private bool _dispatched;

            private async void Dispatch() {
                if (_dispatched) return;
                _dispatched = true;

                await Task.Delay(100);

                Application.Current.Dispatcher.Invoke(() => {
                    UpdateInternal?.Invoke(this, new EventArgs());
                    _dispatched = false;
                });
            }

            public void Dispose() {
                UnsetFsWatcher();
            }
        }

        public void Dispose() {
            foreach (var value in _watchers.Values) {
                value.Dispose();
            }
            _watchers.Clear();
        }

        private Dictionary<string, ContentWatcher> _watchers;

        public virtual ContentWatcher Watcher(params string[] name) {
            if (_watchers == null) {
                _watchers = new Dictionary<string, ContentWatcher>();
            }

            var nameCombined = Path.Combine(name);

            if (_watchers.ContainsKey(nameCombined)) return _watchers[nameCombined];

            var directory = GetSubdirectoryFilename(nameCombined);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            _watchers[nameCombined] = new ContentWatcher(directory);
            return _watchers[nameCombined];
        }

        protected abstract string GetSubdirectoryFilename(string name);
    }
}
