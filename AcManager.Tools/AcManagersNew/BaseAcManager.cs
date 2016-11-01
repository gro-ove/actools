using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    /// <summary>
    /// Non-templated base for static fields.
    /// </summary>
    public abstract class BaseAcManagerNew : NotifyPropertyChanged {
        public static int OptionAcObjectsLoadingConcurrency = 3;
    }

    /// <summary>
    /// Most base version of AcManager, doesn’t have concept of file (so could be used, for example, for online servers).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BaseAcManager<T> : BaseAcManagerNew, IAcManagerNew, IAcWrapperLoader, IEnumerable<T> where T : AcObjectNew {
        [NotNull]
        protected readonly AcWrapperObservableCollection InnerWrappersList;

        public bool CheckIfScanningInProcess() => IsScanning;

        protected bool IsScanning;
        protected bool LoadingReset;
        private bool _isScanned;
        private bool _isLoaded;

        public bool IsScanned {
            get { return _isScanned; }
            protected set {
                if (value == _isScanned) return;
                _isScanned = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoaded {
            get { return _isLoaded; }
            protected set {
                if (value == _isLoaded) return;
                _isLoaded = value;
                OnPropertyChanged();

                if (value) {
                    LoadedCount = InnerWrappersList.Count;
                }
            }
        }

        internal BaseAcManager() {
            // ReSharper disable once VirtualMemberCallInContructor
            InnerWrappersList = CreateCollection();
            InnerWrappersList.ListenersChanged += WrappersListListenersChanged;
        }

        /// <summary>
        /// Called in constructor! So, you know, no weird business.
        /// </summary>
        /// <returns>Collection (could be something different from AcWrapperObservableCollection for some special cases)</returns>
        [NotNull]
        protected virtual AcWrapperObservableCollection CreateCollection() {
            return new AcWrapperObservableCollection();
        }

        protected readonly object ScanningLock = new object();

        private void EnsureScanned() {
            lock (ScanningLock) {
                if (!IsScanned && !IsScanning) {
                    Scan();
                }
            }
        }
        
        public IAcWrapperObservableCollection WrappersList {
            get {
                EnsureScanned();
                return InnerWrappersList;
            }
        }

        public IAcObjectList WrappersAsIList {
            get {
                EnsureScanned();
                return InnerWrappersList;
            }
        }

        [NotNull]
        public IEnumerable<T> LoadedOnly => WrappersList.Select(x => x.Value).OfType<T>();

        [NotNull]
        // [Obsolete]?
        public IEnumerable<T> EnabledOnly => _enabledOnlyList ?? WrappersList.Select(x => x.Value).Where(x => x.Enabled).OfType<T>();

        private AcEnabledOnlyCollection<T> _enabledOnlyList;

        /// <summary>
        /// Only loaded and enabled items, in order.
        /// </summary>
        public AcEnabledOnlyCollection<T> EnabledOnlyCollection => _enabledOnlyList ?? (_enabledOnlyList = new AcEnabledOnlyCollection<T>(WrappersList));

        private void WrappersListListenersChanged(object sender, ListenersChangedEventHandlerArgs args) {
            if (args.OldListenersCount == 0 && !IsLoaded) {
                EnsureLoadedAsync().Forget();
            }
        }

        public virtual IAcManagerScanWrapper ScanWrapper { get; set; }
        
        public void Scan() {
            var scanned = _isScanned;
            lock (ScanningLock) {
                if (_isScanned && !scanned) {
                    return;
                }

                if (ScanWrapper == null) {
                    ActualScan();
                } else {
                    ScanWrapper.AcManagerScan();
                }
            }
        }

        public virtual void ActualScan() {
            if (IsScanning) throw new Exception(@"Scanning already in process");

            IsLoaded = false;
            IsScanning = true;

            try {
                foreach (var obj in InnerWrappersList.Select(x => x.Value).OfType<T>()) {
                    obj.Outdate();
                }

                InnerWrappersList.ReplaceEverythingBy(ScanInner().Select(x => new AcItemWrapper(this, x)));
            } catch(Exception e) {
                Logging.Error($"[{GetType().Name}] Scanning error: {e}");
                InnerWrappersList.Clear();
                throw;
            } finally {
                IsScanning = false;
                IsScanned = true;
            }
        }

        protected virtual bool Filter(string filename) {
            return true;
        }

        [ItemNotNull]
        protected abstract IEnumerable<AcPlaceholderNew> ScanInner();

        public void EnsureLoaded() {
            EnsureScanned();
            if (!IsLoaded) {
                Load();
            }
        }

        private Task _loadingTask;

        public virtual async Task EnsureLoadedAsync() {
            EnsureScanned();
            if (!IsLoaded) {
                await (_loadingTask ?? (_loadingTask = LoadAsync()));
                _loadingTask = null;
            }
        }

        public void Rescan() {
            Scan();
            if (InnerWrappersList.HasListeners) {
                Load();
            }
        }

        public virtual async Task RescanAsync() {
            Scan();
            if (InnerWrappersList.HasListeners) {
                await LoadAsync();
            }
        }

        private readonly object _loadLock = new object();

        protected void Load() {
            var start = Stopwatch.StartNew();
            lock (_loadLock) {
                foreach (var item in WrappersList.Where(x => !x.IsLoaded)) {
                    item.Value = CreateAndLoadAcObject(item.Value.Id, item.Value.Enabled);
                }

                IsLoaded = true;
                ListReady();
                
                if (GetType() != typeof(CarSkinsManager)) {
                    Logging.Write($"[{GetType().Name}] Loading finished: {WrappersList.Count} objects, {start.ElapsedMilliseconds} ms");
                }
            }
        }

        protected void ResetLoading() {
            LoadingReset = true;
        }

        private int _loadedCount;

        public int LoadedCount {
            get { return _loadedCount; }
            set {
                if (Equals(value, _loadedCount)) return;
                _loadedCount = value;
                OnPropertyChanged();
            }
        }

        protected virtual async Task LoadAsync() {
            var start = Stopwatch.StartNew();

            LoadingReset = false;
            await WrappersList.Select(async x => {
                try {
                    if (x.IsLoaded) return;

                    var loaded = await Task.Run(() => CreateAndLoadAcObject(x.Value.Id, x.Value.Enabled, false));
                    if (x.IsLoaded) return;

                    x.Value = loaded;
                    loaded.PastLoad();
                } finally {
                    LoadedCount++;
                }
            }).WhenAll(SettingsHolder.Content.LoadingConcurrency);

            IsLoaded = true;
            ListReady();

            Logging.Write($"[{GetType().Name}] Async loading finished: {WrappersList.Count} objects, {start.ElapsedMilliseconds} ms");

            if (LoadingReset) {
                Load();
            }
        }
        
        public void Reload([NotNull]string id) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            var wrapper = GetWrapperById(id);
            if (wrapper == null) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));
            wrapper.Value = CreateAndLoadAcObject(wrapper.Value.Id, wrapper.Value.Enabled);
        }
        
        protected void ListReady() {
            InnerWrappersList.Ready();
            OnListUpdate();
        }

        public void UpdateList() {
            InnerWrappersList.Update();
            if (InnerWrappersList.IsReady) {
                OnListUpdate();
            }
        }

        protected virtual void OnListUpdate() {}

        protected void RemoveFromList([NotNull]string id) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            var wrapper = GetWrapperById(id);
            if (wrapper == null) return;
            if (wrapper.IsLoaded) {
                ((AcObjectNew)wrapper.Value).Outdate();
            }
            InnerWrappersList.Remove(wrapper);
            UpdateList();
            ResetLoading();
        }

        protected void ReplaceInList([NotNull]string oldId, AcItemWrapper newItem) {
            if (oldId == null) throw new ArgumentNullException(nameof(oldId));
            var wrapper = GetWrapperById(oldId);
            if (wrapper == null) return;
            if (wrapper.IsLoaded) {
                ((AcObjectNew)wrapper.Value).Outdate();
            }
            InnerWrappersList.Replace(wrapper, newItem);
            UpdateList();
            ResetLoading();
        }
        
        [CanBeNull]
        public AcItemWrapper GetWrapperById([NotNull] string id) {
            for (var i = 0; i < WrappersList.Count; i++) {
                var x = WrappersList[i];
                if (x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) return x;
            }
            return null;
        }

        [NotNull]
        public T EnsureWrapperLoaded([NotNull] AcItemWrapper wrapper, out bool isFreshlyLoaded) {
            if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));

            lock (wrapper) {
                if (wrapper.IsLoaded) {
                    isFreshlyLoaded = false;
                    return (T)wrapper.Value;
                }

                var value = CreateAndLoadAcObject(wrapper.Id, wrapper.Value.Enabled);
                wrapper.Value = value;
                isFreshlyLoaded = true;
                return value;
            }
        }

        [NotNull]
        public T EnsureWrapperLoaded([NotNull] AcItemWrapper wrapper) {
            if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));
            
            lock (wrapper) {
                if (wrapper.IsLoaded) {
                    return (T)wrapper.Value;
                }

                var value = CreateAndLoadAcObject(wrapper.Id, wrapper.Value.Enabled);
                wrapper.Value = value;
                return value;
            }
        }

        [NotNull]
        public async Task<T> EnsureWrapperLoadedAsync([NotNull] AcItemWrapper wrapper) {
            if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));

            if (wrapper.IsLoaded) {
                return (T)wrapper.Value;
            }

            var value = await Task.Run(() => CreateAndLoadAcObject(wrapper.Id, wrapper.Value.Enabled, false));
            wrapper.Value = value;
            return value;
        }

        [CanBeNull]
        public T GetById([NotNull, LocalizationRequired(false)] string id, out bool isFreshlyLoaded) {
            var wrapper = GetWrapperById(id);
            if (wrapper != null) {
                return EnsureWrapperLoaded(wrapper, out isFreshlyLoaded);
            }

            isFreshlyLoaded = false;
            return null;
        }

        [CanBeNull]
        public virtual T GetById([NotNull, LocalizationRequired(false)] string id) {
            var wrapper = GetWrapperById(id);
            return wrapper == null ? null : EnsureWrapperLoaded(wrapper);
        }

        public bool CheckIfIdExists([NotNull, LocalizationRequired(false)] string id) {
            return GetWrapperById(id) != null;
        }

        [ItemCanBeNull]
        public virtual async Task<T> GetByIdAsync([NotNull, LocalizationRequired(false)] string id) {
            var wrapper = GetWrapperById(id);
            return wrapper != null ? await EnsureWrapperLoadedAsync(wrapper) : null;
        }

        [CanBeNull]
        public T GetFirstOrNull() {
            var wrapper = WrappersList.FirstOrDefault();
            return wrapper == null ? null : EnsureWrapperLoaded(wrapper);
        }
        
        public IAcObjectNew GetObjectById(string id) {
            return GetById(id);
        }

        [CanBeNull]
        public virtual T GetDefault() {
            return GetFirstOrNull();
        }

        [NotNull]
        protected virtual T CreateAndLoadAcObject([NotNull, LocalizationRequired(false)] string id, bool enabled, bool withPastLoad = true) {
            var result = CreateAcObject(id, enabled);
            result.Load();
            if (withPastLoad) {
                result.PastLoad();
            }

            return result;
        }

        [NotNull]
        protected AcPlaceholderNew CreateAcPlaceholder([NotNull, LocalizationRequired(false)] string id, bool enabled) {
            return new AcPlaceholderNew(id, enabled);
        }

        [NotNull]
        protected virtual T CreateAcObject([NotNull] string id, bool enabled) {
            return (T)Activator.CreateInstance(typeof(T), this, id, enabled);
        }

        void IAcWrapperLoader.Load([NotNull] string id) {
            var wrapper = GetWrapperById(id);
            if (wrapper == null) return;
            EnsureWrapperLoaded(wrapper);
        }

        async Task IAcWrapperLoader.LoadAsync([NotNull] string id) {
            var wrapper = GetWrapperById(id);
            if (wrapper == null) return;
            await EnsureWrapperLoadedAsync(wrapper);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() {
            return InnerWrappersList.Select(x => x.Value).OfType<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return InnerWrappersList.Select(x => x.Value).OfType<T>().GetEnumerator();
        }
    }
}