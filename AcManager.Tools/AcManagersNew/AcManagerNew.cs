// #define LOGGING

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

#if LOGGING
using System.Diagnostics;
using AcTools.Utils.Helpers;
#endif

namespace AcManager.Tools.AcManagersNew {
    public interface ICreatingManager {
        /// <summary>
        /// Create a new object.
        /// </summary>
        /// <param name="id">If null, random/next ID will be used instead.</param>
        /// <exception cref="Exception">Can’t create an object because of whatever reason.</exception>
        /// <returns>Created object if not cancelled.</returns>
        [CanBeNull]
        IAcObjectNew AddNew([CanBeNull] string id = null);
    }

    /// <summary>
    /// “Standart” version — files & watching.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AcManagerNew<T> : FileAcManager<T>, IDirectoryListener, IWatchingChangeApplier, IIgnorer where T : AcCommonObject {
        private bool _subscribed;

        public override void ActualScan() {
            base.ActualScan();

            if (_subscribed || !IsScanned || Directories == null) return;
            _subscribed = true;
            Directories.Subscribe(this);
        }

        protected virtual string GetLocationByFilename(string filename, out bool inner) {
            if (Directories == null) {
                inner = false;
                return null;
            }
            return Directories.GetLocationByFilename(filename, out inner);
        }

        protected override async Task MoveOverrideAsync(string oldId, string newId, string oldLocation, string newLocation,
                IEnumerable<Tuple<string, string>> attachedOldNew, bool newEnabled) {
            using (IgnoreChanges()) {
                await base.MoveOverrideAsync(oldId, newId, oldLocation, newLocation, attachedOldNew, newEnabled);
            }
        }

        protected override async Task CloneOverrideAsync(string oldId, string newId, string oldLocation, string newLocation,
                IEnumerable<Tuple<string, string>> attachedOldNew, bool newEnabled) {
            using (IgnoreChanges()) {
                await base.CloneOverrideAsync(oldId, newId, oldLocation, newLocation, attachedOldNew, newEnabled);
            }
        }

        protected override async Task DeleteOverrideAsync(string id, string location, IEnumerable<string> attached) {
            using (IgnoreChanges()) {
                await base.DeleteOverrideAsync(id, location, attached);
            }
        }

        protected override Task DeleteOverrideAsync(IEnumerable<Tuple<string, string, IEnumerable<string>>> list) {
            using (IgnoreChanges()) {
                return base.DeleteOverrideAsync(list);
            }
        }

        protected override async Task CleanSpaceOverrideAsync(string id, string location) {
            // using (IgnoreChanges()) { // TODO?
                await base.CleanSpaceOverrideAsync(id, location);
            // }
        }

        private readonly Dictionary<string, WatchingTask> _watchingTasks = new Dictionary<string, WatchingTask>();

        protected WatchingTask GetWatchingTask(string location) {
            lock (_watchingTasks) {
                if (!_watchingTasks.ContainsKey(location)) {
                    _watchingTasks[location] = new WatchingTask(location, this);
                }

                return _watchingTasks[location];
            }
        }

        void IWatchingChangeApplier.ApplyChange(string dir, WatchingChange change) {
#if LOGGING
            Debug.WriteLine($"ACMGR [NEW]: IWatchingChangeApplier.ApplyChange({dir}, {change.Type})\n" +
                            $"    ORIGINAL FILENAME: {change.FullFilename}\n" +
                            $"    NEW LOCATION: {change.NewLocation}");
#endif
            string id;
            try {
                id = Directories.GetId(dir);
            } catch (Exception) {
                // can’t get location from id
                return;
            }

            var obj = GetById(id, out var isFreshlyLoaded);

#if LOGGING
            Debug.WriteLine($"    id: {id}; object: {obj}; location: {obj?.Location}");
            if (obj != null && !obj.Location.Equals(dir, StringComparison.OrdinalIgnoreCase)) {
                if (change.Type == WatcherChangeTypes.Created) {
                    Debug.WriteLine(@"    wrong location, removed");
                    RemoveFromList(obj.Id);
                } else {
                    Debug.WriteLine(@"    wrong location, nulled");
                }
                obj = null;
            }
#else
            if (obj != null && !obj.Location.Equals(dir, StringComparison.OrdinalIgnoreCase)) {
                if (change.Type == WatcherChangeTypes.Created) {
                    RemoveFromList(obj.Id);
                }
                obj = null;
            }
#endif

            switch (change.Type) {
                case WatcherChangeTypes.Changed:
                    if (obj != null && !isFreshlyLoaded &&
                            (change.FullFilename == null || !obj.HandleChangedFile(change.FullFilename))) {
                        obj.Reload();
                        UpdateList(true);
                    }
                    break;

                case WatcherChangeTypes.Created:
                    if (obj != null) {
                        if (!isFreshlyLoaded) {
                            obj.Reload();
                            UpdateList(true);
                        }
                    } else if (FileUtils.Exists(dir) && Filter(dir)) {
                        id = Directories.GetId(FileUtils.GetOriginalFilename(dir));
                        obj = CreateAndLoadAcObject(id, Directories.CheckIfEnabled(dir));
                        InnerWrappersList.Add(new AcItemWrapper(this, obj));
                        UpdateList(true);
                    }
                    break;

                case WatcherChangeTypes.Deleted:
                    if (obj != null) {
                        if (FileUtils.Exists(dir) && Filter(dir)) {
                            if (!isFreshlyLoaded) {
                                obj.Reload();
                                UpdateList(true);
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
                                // TODO: why without inversion? could be an issue? some explanation is needed
                                obj.Reload();
                            }
                            break;
                        }

                        RemoveFromList(obj.Id);
                    }

                    if (FileUtils.Exists(change.NewLocation)) {
                        obj = CreateAndLoadAcObject(Directories.GetId(change.NewLocation), Directories.CheckIfEnabled(change.NewLocation));
                        InnerWrappersList.Add(new AcItemWrapper(this, obj));
                        UpdateList(true);
                    }

                    break;

                case WatcherChangeTypes.All:
                    Logging.Warning("WatcherChangeTypes.All!");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

#if LOGGING
            Debug.WriteLine(@"    current list: " + InnerWrappersList.Select(x => x.Value.Id).JoinToString(", "));
#endif
        }

        private void OnChanged(string fullPath) {
            // ignore all directories changes — we’ll receive events on sublevel anyway
            try {
                if (FileUtils.IsDirectory(fullPath)) return;
            } catch (Exception) {
                return;
            }

            var objectLocation = GetLocationByFilename(fullPath, out var inner)?.ToLowerInvariant();
            if (objectLocation == null) return;

            var objectId = Directories.GetId(objectLocation);
            if (!Filter(objectId, objectLocation)) {
                if (GetWrapperById(objectId) != null) {
                    GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Deleted, null, fullPath);
                }
                return;
            }

            if (inner && ShouldSkipFile(objectLocation, fullPath)) return;
            GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Changed, null, fullPath);
        }

        void IDirectoryListener.FileOrDirectoryChanged(object sender, FileSystemEventArgs e) {
            if (ShouldIgnoreChanges()) return;
            OnChanged(e.FullPath);
        }

        protected virtual void OnCreatedIgnored(string filename) {}

        private void OnCreated(string fullPath) {
            var objectLocation = GetLocationByFilename(fullPath, out var inner)?.ToLowerInvariant();
            if (objectLocation == null) return;

            var objectId = Directories.GetId(objectLocation);
            if (!Filter(objectId, objectLocation)) {
                if (GetWrapperById(objectId) != null) {
                    GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Deleted, null, fullPath);
                }else {
                    OnCreatedIgnored(fullPath);
                }
                return;
            }

            // some very special case for Kunos DLC content
            if (objectId.StartsWith(@"ks_") && GetWrapperById(objectId) == null) {
                GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Created, null, fullPath);
                return;
            }

            if (inner && ShouldSkipFile(objectLocation, fullPath)) return;
            GetWatchingTask(objectLocation).AddEvent(inner ? WatcherChangeTypes.Changed : WatcherChangeTypes.Created, null, fullPath);
        }

        void IDirectoryListener.FileOrDirectoryCreated(object sender, FileSystemEventArgs e) {
            if (ShouldIgnoreChanges()) return;

            // special case for whole directory being created
            if (e.Name == null) {
                ActionExtension.InvokeInMainThreadAsync(() => {
                    foreach (var f in FileUtils.GetFilesAndDirectories(e.FullPath)) {
                        OnCreated(f);
                    }
                });
                return;
            }

            OnCreated(e.FullPath);
        }

        protected virtual void OnDeletedIgnored(string filename, string pseudoId) {}

        private void OnDeleted(string fullPath) {
            var objectLocation = GetLocationByFilename(fullPath, out var inner)?.ToLowerInvariant();
            if (objectLocation == null) return;

            var objectId = Directories.GetId(objectLocation);
            if (!Filter(objectId, objectLocation)) {
                if (GetWrapperById(objectId) != null) {
                    GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Deleted, null, fullPath);
                } else {
                    OnDeletedIgnored(fullPath, objectId);
                }
                return;
            }

            if (inner && ShouldSkipFile(objectLocation, fullPath)) return;
            GetWatchingTask(objectLocation).AddEvent(inner ? WatcherChangeTypes.Changed : WatcherChangeTypes.Deleted, null, fullPath);
        }

        void IDirectoryListener.FileOrDirectoryDeleted(object sender, FileSystemEventArgs e) {
            if (ShouldIgnoreChanges()) return;

            // special case for whole directory being deleted
            if (e.Name == null) {
                var state = Directories.CheckIfEnabled(e.FullPath);
                ActionExtension.InvokeInMainThreadAsync(() => {
                    while (InnerWrappersList.Remove(InnerWrappersList.FirstOrDefault(x => x.Value.Enabled == state))) {}
                });
                return;
            }

            OnDeleted(e.FullPath);
        }

        void IDirectoryListener.FileOrDirectoryRenamed(object sender, RenamedEventArgs e) {
            if (ShouldIgnoreChanges()) return;

            OnDeleted(e.OldFullPath);
            OnCreated(e.FullPath);
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
            return filename.EndsWith(@".tmp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
