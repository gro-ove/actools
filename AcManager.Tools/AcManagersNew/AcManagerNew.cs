using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    public class IgnoringHolder : IDisposable {
        internal event EventHandler Disposed;

        public void Dispose() {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// “Standart” version — files & watching.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AcManagerNew<T> : FileAcManager<T>, IDirectoryListener, IWatchingChangeApplier where T : AcCommonObject {
        private bool _subscribed;

        public override void ActualScan() {
            base.ActualScan();

            if (_subscribed || !IsScanned) return;
            _subscribed = true;
            Directories.Subscribe(this);
        }

        protected virtual string GetObjectLocation(string filename, out bool inner) {
            var minLength = Math.Min(Directories.EnabledDirectory.Length,
                Directories.DisabledDirectory?.Length ?? int.MaxValue);

            inner = false;
            while (filename.Length > minLength) {
                var parent = Path.GetDirectoryName(filename);
                if (parent == null) return null;

                if (parent == Directories.EnabledDirectory || parent == Directories.DisabledDirectory) {
                    return filename;
                }

                inner = true;
                filename = parent;
            }

            return null;
        }

        private readonly Dictionary<string, WatchingTask> _watchingTasks = new Dictionary<string, WatchingTask>();

        [MethodImpl(MethodImplOptions.Synchronized)]
        private WatchingTask GetWatchingTask(string location) {
            if (!_watchingTasks.ContainsKey(location)) {
                _watchingTasks[location] = new WatchingTask(location, this);
            }

            return _watchingTasks[location];
        }

        void IWatchingChangeApplier.ApplyChange(string dir, WatchingChange change) {
            Debug.WriteLine($"ACMGR [NEW]: IWatchingChangeApplier.ApplyChange({dir}, {change.Type})\n" +
                            $"    ORIGINAL FILENAME: {change.FullFilename}\n" +
                            $"    NEW LOCATION: {change.NewLocation}");

            string id;
            try {
                id = LocationToId(dir);
            } catch (Exception) {
                // can't get location from id
                return;
            }

            bool isFreshlyLoaded;
            var obj = GetById(id, out isFreshlyLoaded);

            Debug.WriteLine($"    object: {obj}; location: {obj?.Location}");
            if (obj != null && !obj.Location.Equals(dir, StringComparison.OrdinalIgnoreCase)) {
                if (change.Type == WatcherChangeTypes.Created) {
                    Debug.WriteLine("    wrong location, removed");
                    RemoveFromList(obj.Id);
                } else {
                    Debug.WriteLine("    wrong location, nulled");
                }
                obj = null;
            }

            switch (change.Type) {
                case WatcherChangeTypes.Changed:
                    if (obj != null && !isFreshlyLoaded &&
                            (change.FullFilename == null || !obj.HandleChangedFile(change.FullFilename))) {
                        obj.Reload();
                    }
                    break;

                case WatcherChangeTypes.Created:
                    if (obj != null) {
                        if (!isFreshlyLoaded) {
                            obj.Reload();
                        }
                    } else if (FileUtils.Exists(dir)) {
                        obj = CreateAndLoadAcObject(id, Directories.CheckIfEnabled(dir));
                        InnerWrappersList.Add(new AcItemWrapper(this, obj));
                        UpdateList();
                    }
                    break;

                case WatcherChangeTypes.Deleted:
                    if (obj != null) {
                        if (FileUtils.Exists(dir)) {
                            if (!isFreshlyLoaded) {
                                obj.Reload();
                            }
                        } else {
                            RemoveFromList(obj.Id);
                        }
                    }
                    break;

                case WatcherChangeTypes.Renamed:
                    if (obj != null) {
                        if (dir == change.NewLocation) {
                            if (isFreshlyLoaded) {
                                obj.Reload();
                            }
                            break;
                        }

                        RemoveFromList(obj.Id);
                    }

                    if (FileUtils.Exists(change.NewLocation)) {
                        obj = CreateAndLoadAcObject(LocationToId(change.NewLocation), Directories.CheckIfEnabled(change.NewLocation));
                        InnerWrappersList.Add(new AcItemWrapper(this, obj));
                        UpdateList();
                    }

                    break;

                case WatcherChangeTypes.All:
                    Logging.Warning("WatcherChangeTypes.All!");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            Debug.WriteLine("    current list: " + InnerWrappersList.Select(x => x.Value.Id).JoinToString(", "));
        }

        void IDirectoryListener.FileOrDirectoryChanged(object sender, FileSystemEventArgs e) {
            if (ShouldIgnoreChanges()) return;

            // ignore all directories changes — we'll receive events on sublevel anyway
            if (File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory)) return;

            bool inner;
            var objectLocation = GetObjectLocation(e.FullPath, out inner)?.ToLowerInvariant();
            if (objectLocation == null || !Filter(objectLocation) || inner && ShouldSkipFile(objectLocation, e.FullPath)) return;
            GetWatchingTask(objectLocation).AddEvent(inner ? WatcherChangeTypes.Changed : e.ChangeType, null, e.FullPath);
        }

        void IDirectoryListener.FileOrDirectoryCreated(object sender, FileSystemEventArgs e) {
            if (ShouldIgnoreChanges()) return;

            bool inner;
            var objectLocation = GetObjectLocation(e.FullPath, out inner)?.ToLowerInvariant();
            if (objectLocation == null || !Filter(objectLocation) || inner && ShouldSkipFile(objectLocation, e.FullPath)) return;
            GetWatchingTask(objectLocation).AddEvent(inner ? WatcherChangeTypes.Changed : e.ChangeType, null, e.FullPath);
        }

        private void RemoveEverythingFromListByEnabledState(bool state) {
            Application.Current.Dispatcher.InvokeAsync(() => {
                while (InnerWrappersList.Remove(InnerWrappersList.FirstOrDefault(x => x.Value.Enabled == state))) { }
            });
        }

        void IDirectoryListener.FileOrDirectoryDeleted(object sender, FileSystemEventArgs e) {
            if (ShouldIgnoreChanges()) return;

            if (e.Name == null) {
                var state = Directories.CheckIfEnabled(e.FullPath);
                RemoveEverythingFromListByEnabledState(state);
                return;
            }

            bool inner;
            var objectLocation = GetObjectLocation(e.FullPath, out inner)?.ToLowerInvariant();
            if (objectLocation == null || !Filter(objectLocation) || inner && ShouldSkipFile(objectLocation, e.FullPath)) return;
            GetWatchingTask(objectLocation).AddEvent(inner ? WatcherChangeTypes.Changed : e.ChangeType, null, e.FullPath);
        }

        void IDirectoryListener.FileOrDirectoryRenamed(object sender, RenamedEventArgs e) {
            if (ShouldIgnoreChanges()) return;

            bool inner;
            var objectLocation = GetObjectLocation(e.OldFullPath, out inner)?.ToLowerInvariant();
            if (objectLocation == null || !Filter(objectLocation)) return;

            if (!inner) {
                GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Renamed, GetObjectLocation(e.FullPath, out inner), null);
                return;
            }

            if (!ShouldSkipFile(objectLocation, e.OldFullPath)) {
                GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Changed, null, e.OldFullPath);
            }

            if (!ShouldSkipFile(objectLocation, e.FullPath)) {
                GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Changed, null, e.FullPath);
            }

        }

        private DateTime _ignoreChanges;
        private readonly List<IgnoringHolder> _ignoringHolders = new List<IgnoringHolder>();

        private bool ShouldIgnoreChanges() {
            return _ignoringHolders.Count > 0 || DateTime.Now < _ignoreChanges;
        }

        public void IgnoreChangesForAWhile(double timeout = 0.5) {
            _ignoreChanges = DateTime.Now + TimeSpan.FromSeconds(timeout);
        }

        public IgnoringHolder IgnoreChanges() {
            var holder = new IgnoringHolder();
            holder.Disposed += (sender, args) => {
                _ignoringHolders.Remove(sender as IgnoringHolder);
                IgnoreChangesForAWhile();
            };
            _ignoringHolders.Add(holder);
            return holder;
        }

        protected virtual bool ShouldSkipFile([NotNull]string objectLocation, [NotNull]string filename) {
            return filename.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
