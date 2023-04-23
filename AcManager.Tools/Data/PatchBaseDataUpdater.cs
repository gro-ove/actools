using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers.InnerHelpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class PatchDataEntry : Displayable, IWithId {
        [NotNull]
        public string Id { get; private set; } = string.Empty;

        public void Initialize([NotNull] string id, [NotNull] PatchBaseDataUpdater updater) {
            Id = id;
            Parent = updater;

            var destination = GetDestinationFilename();
            if (destination == null || File.Exists(destination)) {
                InstalledVersion = Parent.Storage.Get<string>($"{id}:installed");
            }
        }

        public void Refresh() {
            var destination = GetDestinationFilename();
            if (destination != null && IsInstalled && !File.Exists(destination)) {
                InstalledVersion = null;
            }
        }

        private string _name;
        private bool _isAvailable;

        public override string DisplayName {
            get {
                UpdateState();
                return _name;
            }
            set { }
        }

        [CanBeNull, JsonProperty("notes")]
        public string Notes { get; protected set; }

        [CanBeNull, JsonProperty("notesVersion")]
        public string NotesVersion { get; protected set; }

        [CanBeNull, JsonProperty("dateRelease")]
        public string DateRelease { get; protected set; }

        [CanBeNull, JsonProperty("author")]
        public string Author { get; protected set; }

        [CanBeNull, JsonProperty("version")]
        public string LatestVersion { get; protected set; }

        [JsonProperty("size")]
        public long Size { get; private set; }

        public bool IsAvailable {
            get {
                UpdateState();
                return _isAvailable;
            }
        }

        public string DisplaySize => Size.ToReadableSize();

        private bool _gettingState;

        private async void UpdateState() {
            if (_gettingState) return;
            _gettingState = true;

            var data = await GetStateAsync();
            _name = data?.Item1 ?? Id;
            _isAvailable = data?.Item2 ?? false;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsAvailable));
        }

        private string _installedVersion;

        public string InstalledVersion {
            get => _installedVersion;
            set => Apply(value, ref _installedVersion, () => {
                _installCommand?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(IsNewestInstalled));
                Parent.Storage.Set($"{Id}:installed", value);
            });
        }

        public bool IsInstalled => InstalledVersion != null;
        public bool IsNewestInstalled => InstalledVersion == LatestVersion;

        internal PatchBaseDataUpdater Parent { get; set; }

        private AsyncCommand<string> _installCommand;

        public AsyncCommand<string> InstallCommand => _installCommand ?? (_installCommand = new AsyncCommand<string>(
                s => InstallAsync(s == @"force"),
                s => s == @"force" || InstalledVersion != LatestVersion));

        public async Task InstallAsync(bool force, IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            try {
                await InstallOrThrowAsync(force, progress, cancellation);
            } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                Logging.Error(e);
                NonfatalError.NotifyBackground($"Can’t load piece of patch data “{Id}”", e);
            }
        }

        protected virtual async Task InstallOrThrowAsync(bool force, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (Parent == null || !force && InstalledVersion == LatestVersion) return;

            var s = new AsyncProgressBytesStopwatch();
            progress?.Report(AsyncProgressEntry.Indetermitate);
            var data = await Parent.ApiCache.GetBytesAsync(GetApiUrl(), null, TimeSpan.Zero,
                    new Progress<long>(v => progress?.Report(AsyncProgressEntry.CreateDownloading(v, Size, s))), cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (data == null) {
                throw new InformativeException($"Can’t load piece of patch data “{Id}”");
            }


            if (IsToUnzip) {
                using (var stream = new MemoryStream(data, false))
                using (var archive = new ZipArchive(stream)) {
                    if (!IsDataValid(archive)) {
                        throw new InformativeException($"Data is damaged “{Id}”");
                    }

                    var destination = Parent.GetDestinationDirectory();
                    await Task.Run(() => {
                        foreach (var entry in archive.Entries) {
                            if (!FilterEntry(entry)) {
                                Logging.Error("Skipping invalid entry: " + entry.FullName);
                                continue;
                            }

                            var fullPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                            if (Path.GetFileName(fullPath).Length == 0) continue;
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? "");
                            entry.ExtractToFile(fullPath, true);
                        }
                    });
                }
            } else {
                var destination = GetDestinationFilename();
                if (destination != null) {
                    await Task.Run(() => {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? "");
                        File.WriteAllBytes(destination, data);
                    });
                }
            }

            InstalledVersion = LatestVersion;
        }

        public string GetApiUrl() {
            return $@"{InternalUtils.MainApiDomain}/{Parent.GetBaseUrl()}/{Id}";
        }

        protected virtual bool FilterEntry(ZipArchiveEntry entry) {
            var extension = Path.GetExtension(entry.FullName).ToLowerInvariant();
            return extension == @".ini" || extension == @".kn5"
                    || extension == @".dds" || extension == @".png" || extension == @".jpg" || extension == @".jpeg";
        }

        protected virtual bool IsDataValid(ZipArchive archive) {
            return archive.Entries.FirstOrDefault(x => string.Equals(x.FullName, Id + DestinationExtension, StringComparison.OrdinalIgnoreCase)) != null;
        }

        protected abstract bool IsToUnzip { get; }

        [NotNull, Localizable(false)]
        protected abstract string DestinationExtension { get; }

        [ItemCanBeNull]
        protected abstract Task<Tuple<string, bool>> GetStateAsync();

        [CanBeNull]
        protected virtual string GetDestinationFilename() {
            return Path.Combine(Parent.GetDestinationDirectory(), Id + DestinationExtension);
        }

        [NotNull]
        public string GetDestinationExtension() {
            return DestinationExtension;
        }
    }

    public abstract class CountingNotifyPropertyChanged : IInvokingNotifyPropertyChanged {
        private event PropertyChangedEventHandler PropertyChangedInner;

        protected virtual void OnAnyListenerAdded() { }

        protected virtual void OnAllListenerRemoved() { }

        public event PropertyChangedEventHandler PropertyChanged {
            add {
                if (PropertyChangedInner == null) {
                    OnAnyListenerAdded();
                }
                PropertyChangedInner += value;
            }
            remove {
                PropertyChangedInner -= value;
                if (PropertyChangedInner == null) {
                    OnAllListenerRemoved();
                }
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChangedInner?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void IInvokingNotifyPropertyChanged.OnPropertyChanged(string propertyName) {
            OnPropertyChanged(propertyName);
        }

        protected bool Apply<T>(T value, ref T backendValue, Action onChangeCallback = null, [CallerMemberName] string propertyName = null) {
            return NotifyPropertyChangedExtension.Apply(this, value, ref backendValue, onChangeCallback, propertyName);
        }

        protected bool Apply<T>(T value, StoredValue<T> backendValue, Action onChangeCallback = null, [CallerMemberName] string propertyName = null) {
            return NotifyPropertyChangedExtension.Apply(this, value, backendValue, onChangeCallback, propertyName);
        }
    }

    public abstract class PatchBaseDataUpdater : CountingNotifyPropertyChanged {
        private Storage _storage;
        private ApiCacheThing _apiCache;

        public ApiCacheThing ApiCache => _apiCache ?? (_apiCache = new ApiCacheThing(Path.Combine("Patch", GetCacheDirectoryName()), TimeSpan.FromHours(3d)));

        public Storage Storage {
            get {
                if (_storage == null) {
                    Directory.CreateDirectory(GetDestinationDirectory());
                    _storage = new Storage(Path.Combine(GetDestinationDirectory(), "storage.data"));
                }
                return _storage;
            }
        }

        [Localizable(false)]
        public abstract string GetBaseUrl();

        [Localizable(false)]
        public abstract string GetCacheDirectoryName();

        [Localizable(false)]
        public abstract string GetDestinationDirectory();

        public string SourceRepo => @"https://github.com/ac-custom-shaders-patch/acc-extension-config/";

        public string SourceBbCode => $"[url={BbCodeBlock.EncodeAttribute(GetSourceUrl().IsWebUrl() ? GetSourceUrl() : SourceRepo + GetSourceUrl())}]Original source.[/url]";

        [NotNull, Localizable(false)]
        protected abstract string GetSourceUrl();

        [NotNull]
        protected abstract string GetDescription();

        [NotNull]
        protected abstract string GetTitle();

        public string Description => GetDescription() + @" " + SourceBbCode;
        public string Title => GetTitle();

        public abstract BetterListCollectionView View { get; }

        public abstract StoredValue<bool> InstallAutomatically { get; }

        private string _errorMessage;

        public string ErrorMessage {
            get => _errorMessage;
            set => Apply(value, ref _errorMessage);
        }

        private bool _isLoading;

        public bool IsLoading {
            get => _isLoading;
            set => Apply(value, ref _isLoading);
        }

        private bool _isLoaded;

        public bool IsLoaded {
            get => _isLoaded;
            set => Apply(value, ref _isLoaded);
        }

        private AsyncCommand _installEverythingCommand;

        public AsyncCommand InstallEverythingCommand => _installEverythingCommand ?? (_installEverythingCommand = new AsyncCommand(async () => {
            using (var waiting = WaitingDialog.Create("Downloading…")) {
                var cancellationToken = waiting.CancellationToken;
                var secondaryProgress = new Progress<AsyncProgressEntry>(v => waiting.ReportSecondary(v));
                waiting.SetSecondary(true);

                var list = View.OfType<PatchDataEntry>().ToList();
                for (var i = 0; i < list.Count; i++) {
                    waiting.Report(list[i].DisplayName, i, list.Count);
                    waiting.ReportSecondary(AsyncProgressEntry.Indetermitate);
                    await list[i].InstallAsync(false, secondaryProgress, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) break;
                }
            }
        }, () => AvailableToInstall > 0));

        private DelegateCommand _viewInExplorerCommand;

        public DelegateCommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(
                () => WindowsHelper.ViewDirectory(GetDestinationDirectory()), () => Directory.Exists(GetDestinationDirectory())));

        private int _availableToInstall;

        public int AvailableToInstall {
            get => _availableToInstall;
            set => Apply(value, ref _availableToInstall, () => { _installEverythingCommand?.RaiseCanExecuteChanged(); });
        }

        private int _unavailableCount;

        public int UnavailableCount {
            get => _unavailableCount;
            set => Apply(value, ref _unavailableCount);
        }

        private DelegateCommand _showUnavailableEntriesCommand;

        public DelegateCommand ShowUnavailableEntriesCommand => _showUnavailableEntriesCommand ?? (_showUnavailableEntriesCommand
                = new DelegateCommand(ShowUnavailableMessage));

        private void ShowUnavailableMessage() {
            var list = View.SourceCollection.OfType<PatchDataEntry>().Where(x => !x.IsAvailable).NonNull().ToList();
            if (list.Count == 0) {
                MessageDialog.Show($"Good news: you have everything installed, all {View.Count} entries.", $"{Title} - Unavailable entries",
                        MessageDialogButton.OK);
                return;
            }

            MessageDialog.Show($"{list.OrderBy(x => x.Id).Select(x => $" • [mono]{x.Id}[/mono]").JoinToString(";\n")}.",
                    "Not available",
                    MessageDialogButton.OK);
        }
    }

    public abstract class PatchBaseDataUpdater<T> : PatchBaseDataUpdater, IDirectoryListener where T : PatchDataEntry, new() {
        private ChangeableObservableCollection<T> _list;
        private BetterListCollectionView _view;

        [NotNull]
        public ChangeableObservableCollection<T> List {
            get {
                EnsureListCreated();
                EnsureLoadedAsync();
                return _list;
            }
        }

        private void EnsureListCreated() {
            if (_list == null) {
                _list = new ChangeableObservableCollection<T>();
                _list.ItemPropertyChanged += OnListItemPropertyChanged;
            }
        }

        private void OnListItemPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(PatchDataEntry.IsNewestInstalled)) {
                CountAvailableToInstall();
            }
        }

        private void CountAvailableToInstall() {
            AvailableToInstall = _list.Count(x => x.IsAvailable && !x.IsNewestInstalled);
        }

        public override BetterListCollectionView View {
            get {
                if (_view == null) {
                    _view = new BetterListCollectionView(List) {
                        Filter = x => x is PatchDataEntry v && v.IsAvailable,
                        SortDescriptions = { new SortDescription(nameof(PatchDataEntry.DisplayName), ListSortDirection.Ascending) }
                    };
                }
                return _view;
            }
        }

        private DirectoryWatcher _watcher;

        protected override void OnAnyListenerAdded() {
            _watcher?.Dispose();
            _watcher = new DirectoryWatcher(GetDestinationDirectory(), @"*" + new T().GetDestinationExtension());
            _watcher.Subscribe(this);
        }

        protected override void OnAllListenerRemoved() {
            _watcher?.Dispose();
            _watcher = null;
        }

        public override StoredValue<bool> InstallAutomatically { get; } = Stored.Get("/PatchDataUpdater." + typeof(T).Name, true);

        private readonly TaskCache _cache = new TaskCache();

        protected virtual Task Prepare() {
            return Task.Delay(0);
        }

        protected virtual IEnumerable<string> AutoLoadSelector(string requested, IEnumerable<string> available) {
            return new[] { requested };
        }

        public Task TriggerAutoLoadAsync([CanBeNull] string id, IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default) {
            if (!PatchHelper.OptionPatchSupport || cancellation.IsCancellationRequested) return Task.Delay(0);

            Logging.Debug($"Auto-loading stuff for “{id}” from {Title.ToSentenceMember()} list");
            if (!InstallAutomatically.Value || string.IsNullOrWhiteSpace(id)) return Task.Delay(0);
            return _cache.Get(async () => {
                try {
                    var anything = false;
                    if (IsLoaded && _list?.Count > 0) {
                        Logging.Debug("List already loaded: " + _list.Count);
                        foreach (var toInstall in AutoLoadSelector(id, _list.Select(x => x.Id))) {
                            var item = _list.GetByIdOrDefault(toInstall);
                            if (item != null) {
                                Logging.Debug("ID to install: " + toInstall);
                                anything = true;
                                await item.InstallAsync(false, progress, cancellation);
                            } else {
                                Logging.Debug("Entry is not found: " + toInstall);
                            }
                        }
                    } else {
                        Logging.Debug("Loading entries list…");
                        progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Loading entries list…"));
                        var list = await ApiCache.GetStringAsync($"{InternalUtils.MainApiDomain}/{GetBaseUrl()}", @"list").WithCancellation(cancellation);
                        Logging.Debug("Done: " + cancellation.IsCancellationRequested);
                        if (cancellation.IsCancellationRequested) return;

                        var jObject = JObject.Parse(list);
                        Logging.Debug("Freshly loaded list: " + jObject.Count);
                        foreach (var toInstall in AutoLoadSelector(id, GetKeys(jObject))) {
                            if (jObject[toInstall]?.Type == JTokenType.Object) {
                                Logging.Debug("ID to install: " + toInstall);
                                anything = true;
                                var parsed = jObject[toInstall].ToObject<T>();
                                parsed.Initialize(toInstall, this);
                                await parsed.InstallAsync(false, progress, cancellation);
                            } else {
                                Logging.Debug("Entry is not found: " + toInstall);
                            }
                        }
                    }
                    if (!anything) {
                        Logging.Warning("Nothing found for: " + id);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }, id);
        }

        private IEnumerable<string> GetKeys(JObject jObject) {
            if (jObject == null) yield break;
            foreach (var pair in jObject) {
                yield return pair.Key;
            }
        }

        public Task EnsureLoadedAsync() {
            EnsureListCreated();
            if (_list.Count > 0) return Task.Delay(0);
            return _cache.Get(async () => {
                try {
                    IsLoading = true;
                    IsLoaded = false;

                    await Prepare();
                    var list = await ApiCache.GetStringAsync($"{InternalUtils.MainApiDomain}/{GetBaseUrl()}", @"list");
                    if (list == null) {
                        ErrorMessage = "Can’t download list of entries";
                        UnavailableCount = 0;
                    } else {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, T>>(list);
                        var initialized = await Task.Run(() => parsed.Select(x => {
                            x.Value.Initialize(x.Key, this);
                            return x.Value;
                        }).ToList());
                        UnavailableCount = initialized.Count(x => !x.IsAvailable);
                        _list.ReplaceEverythingBy_Direct(initialized);
                        IsLoaded = true;
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                    _list.Clear();
                    ErrorMessage = e.Message;
                    IsLoaded = false;
                    UnavailableCount = 0;
                } finally {
                    IsLoading = false;
                    CountAvailableToInstall();
                }
            });
        }

        private Busy _refreshBusy = new Busy(true);

        private void Refresh() {
            if (_list == null) return;
            foreach (var item in _list) {
                item.Refresh();
            }
        }

        void IDirectoryListener.FileOrDirectoryChanged(object sender, FileSystemEventArgs e) { }

        void IDirectoryListener.FileOrDirectoryCreated(object sender, FileSystemEventArgs e) { }

        void IDirectoryListener.FileOrDirectoryDeleted(object sender, FileSystemEventArgs e) {
            _refreshBusy.DoDelay(Refresh, 300);
        }

        void IDirectoryListener.FileOrDirectoryRenamed(object sender, RenamedEventArgs e) {
            _refreshBusy.DoDelay(Refresh, 300);
        }
    }
}