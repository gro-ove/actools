using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.InnerHelpers {
    public class DirectoryWatcher : IDisposable {
        public readonly string TargetDirectory;
        private readonly string _filter;

        private FileSystemWatcher _innerWatcher;
        private FileSystemWatcher _helperWatcher;

        private readonly bool _failed;
        private readonly List<IDirectoryListener> _listeners = new List<IDirectoryListener>(1);

        public DirectoryWatcher([NotNull] string directory, string filter = null) {
            TargetDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
            _filter = filter ?? "*";

            var parentDirectory = Path.GetDirectoryName(TargetDirectory);
            if (parentDirectory == null || !Directory.Exists(parentDirectory)) {
                _failed = true;
                Logging.Error("FAILED DUE TO PARENT DIRECTORY MISSING: " + TargetDirectory);
                return;
            }

            _helperWatcher = new FileSystemWatcher {
                Path = parentDirectory,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = Path.GetFileName(TargetDirectory),
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _helperWatcher.Created += OnHelperWatcherSomething;
            _helperWatcher.Renamed += OnHelperWatcherSomething;
            _helperWatcher.Deleted += OnHelperWatcherSomething;
            UpdateInnerWatcher();
        }

        private void OnHelperWatcherSomething(object sender, FileSystemEventArgs e) {
            UpdateInnerWatcher();
        }

        private void UpdateInnerWatcher() {
            if (_failed) return;

            if (Directory.Exists(TargetDirectory)) {
                if (_innerWatcher != null) return;

                Debug.WriteLine("UPDATE INNER WATCHER: CREATE WATCHER FOR " + TargetDirectory);

                _innerWatcher = new FileSystemWatcher {
                    Path = TargetDirectory,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = _filter,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true
                };

                lock (_listeners) {
                    foreach (var listener in _listeners) {
                        _innerWatcher.Changed += listener.FileOrDirectoryChanged;
                        _innerWatcher.Created += listener.FileOrDirectoryCreated;
                        _innerWatcher.Deleted += listener.FileOrDirectoryDeleted;
                        _innerWatcher.Renamed += listener.FileOrDirectoryRenamed;
                        listener.FileOrDirectoryCreated(this, new FileSystemEventArgs(WatcherChangeTypes.Created, TargetDirectory, null));
                    }
                }

                lock (_handlers) {
                    foreach (var h in _handlers) {
                        _innerWatcher.Changed += h.Item1;
                        _innerWatcher.Created += h.Item1;
                        _innerWatcher.Deleted += h.Item1;
                        _innerWatcher.Renamed += h.Item2;
                        h.Item1.Invoke(_innerWatcher, new FileSystemEventArgs(WatcherChangeTypes.Created, TargetDirectory, null));
                    }
                }
            } else if (_innerWatcher != null) {
                Debug.WriteLine("UPDATE INNER WATCHER: REMOVE WATCHER FOR " + TargetDirectory);

                _innerWatcher.EnableRaisingEvents = false;
                _innerWatcher.Dispose();
                _innerWatcher = null;

                lock (_listeners) {
                    foreach (var listener in _listeners) {
                        listener.FileOrDirectoryDeleted(this, new FileSystemEventArgs(WatcherChangeTypes.Deleted, TargetDirectory, null));
                    }
                }

                lock (_handlers) {
                    foreach (var h in _handlers) {
                        h.Item1.Invoke(_innerWatcher, new FileSystemEventArgs(WatcherChangeTypes.Created, TargetDirectory, null));
                    }
                }
            }
        }

        public void Subscribe([NotNull] IDirectoryListener listener) {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            if (_failed) return;

            if (_innerWatcher != null) {
                _innerWatcher.Changed += listener.FileOrDirectoryChanged;
                _innerWatcher.Created += listener.FileOrDirectoryCreated;
                _innerWatcher.Deleted += listener.FileOrDirectoryDeleted;
                _innerWatcher.Renamed += listener.FileOrDirectoryRenamed;
            }

            lock (_listeners) {
                _listeners.Add(listener);
            }
        }

        private readonly List<Tuple<FileSystemEventHandler, RenamedEventHandler>> _handlers =
                new List<Tuple<FileSystemEventHandler, RenamedEventHandler>>();

        public event FileSystemEventHandler Update {
            add {
                if (value == null || _failed || _innerWatcher == null) return;

                void RenamedHandler(object sender, RenamedEventArgs args) {
                    value.Invoke(_innerWatcher, new FileSystemEventArgs(WatcherChangeTypes.Deleted,
                            Path.GetDirectoryName(args.OldFullPath) ?? TargetDirectory, args.OldName));
                    value.Invoke(_innerWatcher, new FileSystemEventArgs(WatcherChangeTypes.Created,
                            Path.GetDirectoryName(args.FullPath) ?? TargetDirectory, args.Name));
                }

                if (_innerWatcher != null) {
                    _innerWatcher.Changed += value;
                    _innerWatcher.Created += value;
                    _innerWatcher.Deleted += value;
                    _innerWatcher.Renamed += RenamedHandler;
                }

                lock (_handlers) {
                    _handlers.Add(Tuple.Create(value, (RenamedEventHandler)RenamedHandler));
                }
            }
            remove {
                Tuple<FileSystemEventHandler, RenamedEventHandler> set;

                lock (_handlers) {
                    set = _handlers.FirstOrDefault(x => x.Item1 == value);
                    if (set == null) return;
                }

                if (_innerWatcher != null) {
                    _innerWatcher.Changed -= value;
                    _innerWatcher.Created -= value;
                    _innerWatcher.Deleted -= value;
                    _innerWatcher.Renamed -= set.Item2;
                }

                lock (_handlers) {
                    _handlers.Remove(set);
                }
            }
        }

        public void Dispose() {
            if (_innerWatcher != null) {
                _innerWatcher.EnableRaisingEvents = false;
                _innerWatcher.Dispose();
                _innerWatcher = null;
            }

            if (_helperWatcher != null) {
                _helperWatcher.EnableRaisingEvents = false;
                _helperWatcher.Dispose();
                _helperWatcher = null;
            }

            lock (_listeners) {
                _listeners.Clear();
            }

            lock (_handlers) {
                _handlers.Clear();
            }
        }
    }
}
