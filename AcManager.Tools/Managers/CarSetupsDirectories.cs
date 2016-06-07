using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Managers.InnerHelpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    internal class CarSetupsDirectories : IAcDirectories, IDirectoryListener {
        public CarSetupsDirectories(CarObject car) {
            EnabledDirectory = FileUtils.GetCarSetupsDirectory(car.Id);
        }

        public string EnabledDirectory { get; }

        public string DisabledDirectory => null;

        public bool Actual => true;

        public IEnumerable<string> GetSubDirectories() {
            throw new NotSupportedException();
        }

        public IEnumerable<string> GetSubDirectories(string searchPattern) {
            throw new NotSupportedException();
        }

        public IEnumerable<string> GetSubFiles(string searchPattern) {
            return Directory.Exists(EnabledDirectory)
                    ? Directory.GetDirectories(EnabledDirectory).SelectMany(x => Directory.GetFiles(x, searchPattern))
                    : new string[0];
        }

        public bool CheckIfEnabled(string location) {
            return true;
        }

        public string GetLocation(string id, bool enabled) {
            return Path.Combine(EnabledDirectory, id);
        }

        private List<IDirectoryListener> _subscribed;

        public void Subscribe(IDirectoryListener listener) {
            if (_subscribed == null) {
                AddDirectoryListener(this);
                _subscribed = new List<IDirectoryListener>(1);
            }

            _subscribed.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TestArgs(FileSystemEventArgs e) {
            return e.FullPath.StartsWith(EnabledDirectory, StringComparison.OrdinalIgnoreCase) ||
                   DisabledDirectory != null && e.FullPath.StartsWith(DisabledDirectory, StringComparison.OrdinalIgnoreCase);
        }

        public void FileOrDirectoryChanged(object sender, FileSystemEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryChanged(sender, e);
            }
        }

        public void FileOrDirectoryCreated(object sender, FileSystemEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryCreated(sender, e);
            }
        }

        public void FileOrDirectoryDeleted(object sender, FileSystemEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryDeleted(sender, e);
            }
        }

        public void FileOrDirectoryRenamed(object sender, RenamedEventArgs e) {
            if (!TestArgs(e)) return;
            foreach (var listener in _subscribed) {
                listener.FileOrDirectoryRenamed(sender, e);
            }
        }

        public void Dispose() { }


        private static void AddDirectoryListener(IDirectoryListener listener) {
            if (_watcher == null) {
                _watcher = new DirectoryWatcher(FileUtils.GetCarSetupsDirectory());
                _watcher.Subscribe(new InternalListener());
                Logging.Write("[CarSetupsDirectories.InternalListener] Start watching…");
            }

            Listeners.Purge();
            Listeners.Add(listener);
        }

        private class InternalListener : IDirectoryListener {
            private bool Prepare() {
                if (_watcher == null) return false;

                Listeners.Purge();
                if (Listeners.Any()) return true;

                DisposeHelper.Dispose(ref _watcher);
                Logging.Write("[CarSetupsDirectories.InternalListener] Stop watching…");
                return false;
            }

            public void FileOrDirectoryChanged(object sender, FileSystemEventArgs e) {
                if (!Prepare()) return;
                foreach (var l in Listeners) {
                    l.FileOrDirectoryChanged(sender, e);
                }
            }

            public void FileOrDirectoryCreated(object sender, FileSystemEventArgs e) {
                if (!Prepare()) return;
                foreach (var l in Listeners) {
                    l.FileOrDirectoryCreated(sender, e);
                }
            }

            public void FileOrDirectoryDeleted(object sender, FileSystemEventArgs e) {
                if (!Prepare()) return;
                foreach (var l in Listeners) {
                    l.FileOrDirectoryDeleted(sender, e);
                }
            }

            public void FileOrDirectoryRenamed(object sender, RenamedEventArgs e) {
                if (!Prepare()) return;
                foreach (var l in Listeners) {
                    l.FileOrDirectoryRenamed(sender, e);
                }
            }
        }

        private static DirectoryWatcher _watcher;
        private static readonly WeakList<IDirectoryListener> Listeners = new WeakList<IDirectoryListener>();
    }
}