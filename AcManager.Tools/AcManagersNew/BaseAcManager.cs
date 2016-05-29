using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
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
    /// Most base version of AcManager, doesn't have concept of file (so could be used, for example, for online servers).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BaseAcManager<T> : BaseAcManagerNew, IAcManagerNew, IAcWrapperLoader where T : AcObjectNew {
        protected readonly AcWrapperObservableCollection InnerWrappersList;
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
        protected virtual AcWrapperObservableCollection CreateCollection() {
            return new AcWrapperObservableCollection();
        }
        
        public IAcWrapperObservableCollection WrappersList {
            get {
                if (!IsScanned) {
                    Scan();
                }

                return InnerWrappersList;
            }
        }

        public IAcObjectList WrappersAsIList {
            get {
                if (!IsScanned) {
                    Scan();
                }

                return InnerWrappersList;
            }
        }

        public IEnumerable<T> LoadedOnly => WrappersList.Select(x => x.Value).OfType<T>();

        private AcLoadedOnlyCollection<T> _loadedOnlyList;

        public AcLoadedOnlyCollection<T> LoadedOnlyCollection => _loadedOnlyList ?? (_loadedOnlyList = new AcLoadedOnlyCollection<T>(WrappersList));

        private void WrappersListListenersChanged(object sender, ListenersChangedEventHandlerArgs args) {
            if (args.OldListenersCount == 0 && !IsLoaded) {
                EnsureLoadedAsync().Forget();
            }
        }

        public virtual IAcManagerScanWrapper ScanWrapper { get; set; }
        
        public void Scan() {
            var scanned = _isScanned;
            lock (InnerWrappersList) {
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
            if (IsScanning) throw new Exception("Scanning already in process");

            IsLoaded = false;
            IsScanning = true;

            try {
                foreach (var obj in InnerWrappersList.Select(x => x.Value).OfType<T>()) {
                    obj.Outdate();
                }

                InnerWrappersList.ReplaceEverythingBy(ScanInner().Select(x => new AcItemWrapper(this, x)));
            } catch(Exception e) {
                Logging.Error($"[MANAGER ({GetType()})] Scanning error: {e}");
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
            if (!IsScanned) {
                Scan();
            }
            if (!IsLoaded) {
                Load();
            }
        }

        private Task _loadingTask;

        public virtual async Task EnsureLoadedAsync() {
            if (!IsScanned) {
                Scan();
            }
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

        private readonly object _loadLocker = new object();

        protected void Load() {
            var start = Stopwatch.StartNew();

            lock (_loadLocker) {
                foreach (var item in WrappersList.Where(x => !x.IsLoaded)) {
                    item.Value = CreateAndLoadAcObject(item.Value.Id, item.Value.Enabled);
                }

                IsLoaded = true;
                ListReady();
                
                if (GetType() != typeof(CarSkinsManager)) {
                    Logging.Write($"{{0}}, loading finished: {WrappersList.Count} objects, {start.ElapsedMilliseconds} ms", GetType());
                }
            }
        }

        protected void ResetLoading() {
            LoadingReset = true;
        }

        protected virtual async Task LoadAsync() {
            var start = Stopwatch.StartNew();

            LoadingReset = false;
            await TaskExtension.WhenAll(WrappersList.Where(x => !x.IsLoaded).Select(async x => {
                var loaded = await Task.Run(() => CreateAndLoadAcObject(x.Value.Id, x.Value.Enabled, false));
                if (x.IsLoaded) return;

                x.Value = loaded;
                loaded.PastLoad();
            }), SettingsHolder.Content.LoadingConcurrency);

            IsLoaded = true;
            ListReady();

            Logging.Write($"{{0}}, async loading finished: {WrappersList.Count} objects, {start.ElapsedMilliseconds} ms", GetType());

            if (LoadingReset) {
                Load();
            }
        }

        [NotNull]
        public static string LocationToId(string directory) {
            var name = Path.GetFileName(directory);
            if (name == null) throw new Exception("Cannot get file name from path");
            return name.ToLower();
        }
        
        public void Reload([NotNull]string id) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            var wrapper = GetWrapperById(id);
            if (wrapper == null) throw new ArgumentException(@"ID is wrong", nameof(id));
            wrapper.Value = CreateAndLoadAcObject(id, wrapper.Value.Enabled);
        }
        
        protected virtual void ListReady() {
            InnerWrappersList.Ready();
        }

        public virtual void UpdateList() {
            ((AcWrapperObservableCollection)WrappersList).Update();
        }

        protected void RemoveFromList([NotNull]string id) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            var wrapper = GetWrapperById(id);
            if (wrapper == null) return;
            if (wrapper.IsLoaded) {
                ((AcObjectNew)wrapper.Value).Outdate();
            }
            InnerWrappersList.Remove(wrapper);
            ResetLoading();
        }
        
        [CanBeNull]
        public AcItemWrapper GetWrapperById([NotNull] string id) {
            if (!IsScanned) {
                Scan();
            }

            for (var i = 0; i < InnerWrappersList.Count; i++) {
                var x = InnerWrappersList[i];
                if (x.Value.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) return x;
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

                var value = CreateAndLoadAcObject(wrapper.Value.Id, wrapper.Value.Enabled);
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

                var value = CreateAndLoadAcObject(wrapper.Value.Id, wrapper.Value.Enabled);
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

            var value = await Task.Run(() => CreateAndLoadAcObject(wrapper.Value.Id, wrapper.Value.Enabled, false));
            wrapper.Value = value;
            return value;
        }

        [CanBeNull]
        public T GetById([NotNull]string id, out bool isFreshlyLoaded) {
            var wrapper = GetWrapperById(id);
            if (wrapper != null) {
                return EnsureWrapperLoaded(wrapper, out isFreshlyLoaded);
            }

            isFreshlyLoaded = false;
            return null;
        }

        [CanBeNull]
        public virtual T GetById([NotNull]string id) {
            var wrapper = GetWrapperById(id);
            return wrapper == null ? null : EnsureWrapperLoaded(wrapper);
        }

        public bool CheckIfIdExists([NotNull]string id) {
            return GetWrapperById(id) != null;
        }

        [ItemCanBeNull]
        public virtual async Task<T> GetByIdAsync([NotNull]string id) {
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
        protected virtual T CreateAndLoadAcObject([NotNull]string id, bool enabled, bool withPastLoad = true) {
            var result = CreateAcObject(id, enabled);
            result.Load();
            if (withPastLoad) {
                result.PastLoad();
            }
            
            return result;
        }

        [NotNull]
        protected AcPlaceholderNew CreateAcPlaceholder([NotNull]string id, bool enabled) {
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
    }
}