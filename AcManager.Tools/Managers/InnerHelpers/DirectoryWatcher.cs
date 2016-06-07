using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.InnerHelpers {
    public class DirectoryWatcher : IDisposable {
        public readonly string TargetDirectory;

        private FileSystemWatcher _innerWatcher;
        private FileSystemWatcher _helperWatcher;

        private readonly bool _failed;
        private readonly List<IDirectoryListener> _listeners = new List<IDirectoryListener>(1);

        public DirectoryWatcher([NotNull] string directory) {
            if (directory == null) throw new ArgumentNullException(nameof(directory));

            TargetDirectory = directory;

            var parentDirectory = Path.GetDirectoryName(TargetDirectory);
            if (parentDirectory == null || !Directory.Exists(parentDirectory)) {
                _failed = true;
                Logging.Warning("DIRECTORY WATCHER FAILED DUE TO PARENT DIRECTORY MISSING: " + TargetDirectory);
                return;
            }

            _helperWatcher = new FileSystemWatcher {
                Path = parentDirectory,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = Path.GetFileName(TargetDirectory),
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _helperWatcher.Created += HelperWatcher_Something;
            _helperWatcher.Renamed += HelperWatcher_Something;
            _helperWatcher.Deleted += HelperWatcher_Something;
            UpdateInnerWatcher();
        }

        private void HelperWatcher_Something(object sender, FileSystemEventArgs e) {
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
                    Filter = "*",
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true
                };

                var content = FileUtils.GetFilesAndDirectories(TargetDirectory).ToList();
                foreach (var listener in _listeners) {
                    _innerWatcher.Changed += listener.FileOrDirectoryChanged;
                    _innerWatcher.Created += listener.FileOrDirectoryCreated;
                    _innerWatcher.Deleted += listener.FileOrDirectoryDeleted;
                    _innerWatcher.Renamed += listener.FileOrDirectoryRenamed;

                    foreach (var sub in content) {
                        listener.FileOrDirectoryCreated(this, new FileSystemEventArgs(WatcherChangeTypes.Created, TargetDirectory, Path.GetFileName(sub)));
                    }
                }
            } else if (_innerWatcher != null) {
                Debug.WriteLine("UPDATE INNER WATCHER: REMOVE WATCHER FOR " + TargetDirectory);

                _innerWatcher.EnableRaisingEvents = false;
                _innerWatcher.Dispose();
                _innerWatcher = null;

                foreach (var listener in _listeners) {
                    listener.FileOrDirectoryDeleted(this, new FileSystemEventArgs(WatcherChangeTypes.Deleted, TargetDirectory, null));
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

            _listeners.Add(listener);
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

            _listeners.Clear();
        }
    }
}
