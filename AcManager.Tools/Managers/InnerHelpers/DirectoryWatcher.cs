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

        public FileSystemWatcher InnerWatcher { get; private set; }

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
                if (InnerWatcher != null) return;

                Debug.WriteLine("UPDATE INNER WATCHER: CREATE WATCHER FOR " + TargetDirectory);

                InnerWatcher = new FileSystemWatcher {
                    Path = TargetDirectory,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = "*",
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true
                };

                var content = FileUtils.GetFilesAndDirectories(TargetDirectory).ToList();
                foreach (var listener in _listeners) {
                    InnerWatcher.Changed += listener.FileOrDirectoryChanged;
                    InnerWatcher.Created += listener.FileOrDirectoryCreated;
                    InnerWatcher.Deleted += listener.FileOrDirectoryDeleted;
                    InnerWatcher.Renamed += listener.FileOrDirectoryRenamed;

                    foreach (var sub in content) {
                        listener.FileOrDirectoryCreated(this, new FileSystemEventArgs(WatcherChangeTypes.Created, TargetDirectory, Path.GetFileName(sub)));
                    }
                }
            } else if (InnerWatcher != null) {
                Debug.WriteLine("UPDATE INNER WATCHER: REMOVE WATCHER FOR " + TargetDirectory);

                InnerWatcher.EnableRaisingEvents = false;
                InnerWatcher.Dispose();
                InnerWatcher = null;

                foreach (var listener in _listeners) {
                    listener.FileOrDirectoryDeleted(this, new FileSystemEventArgs(WatcherChangeTypes.Deleted, TargetDirectory, null));
                }
            }
        }
        
        public void Subscribe([NotNull] IDirectoryListener listener) {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            if (_failed) return;

            if (InnerWatcher != null) {
                InnerWatcher.Changed += listener.FileOrDirectoryChanged;
                InnerWatcher.Created += listener.FileOrDirectoryCreated;
                InnerWatcher.Deleted += listener.FileOrDirectoryDeleted;
                InnerWatcher.Renamed += listener.FileOrDirectoryRenamed;
            }

            _listeners.Add(listener);
        }

        public void Dispose() {
            if (InnerWatcher != null) {
                InnerWatcher.EnableRaisingEvents = false;
                InnerWatcher.Dispose();
                InnerWatcher = null;
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
