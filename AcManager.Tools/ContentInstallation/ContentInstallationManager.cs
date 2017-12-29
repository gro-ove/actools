using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationManager : NotifyPropertyChanged {
        public static bool OptionSaveAndRestoreDownloads = false;

        private static readonly string StorageKey = ".Downloads.List";

        private static ContentInstallationManager _instance;

        public static ContentInstallationManager Instance {
            get {
                if (_instance == null) {
                    _instance = new ContentInstallationManager();
                    _instance.Load();
                }

                return _instance;
            }
        }

        public static IPluginsNavigator PluginsNavigator { get; set; }

        private ContentInstallationManager() {
            DownloadList = new ChangeableObservableCollection<ContentInstallationEntry>();
        }

        private void Load() {
            if (OptionSaveAndRestoreDownloads) {
                foreach (var entry in LoadEntries()) {
                    if (entry.State == ContentInstallationEntryState.Finished) {
                        DownloadList.Add(entry);
                    } else {
                        InstallAsync(entry);
                    }
                }
            }

            DownloadList.ItemPropertyChanged += OnItemPropertyChanged;
            DownloadList.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            Save();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ContentInstallationEntry.Cancelled):
                case nameof(ContentInstallationEntry.DisplayName):
                case nameof(ContentInstallationEntry.FailedCommentary):
                case nameof(ContentInstallationEntry.FailedMessage):
                case nameof(ContentInstallationEntry.FileName):
                case nameof(ContentInstallationEntry.InformationUrl):
                case nameof(ContentInstallationEntry.InputPassword):
                case nameof(ContentInstallationEntry.IsCancelling):
                case nameof(ContentInstallationEntry.IsPaused):
                case nameof(ContentInstallationEntry.LocalFilename):
                case nameof(ContentInstallationEntry.State):
                case nameof(ContentInstallationEntry.Version):
                    Save();
                    break;
                case nameof(ContentInstallationEntry.IsDeleted):
                    // No reason to save here, list will be changed as well
                    if (sender is ContentInstallationEntry entry) {
                        DownloadList.Remove(entry);
                    }
                    break;
            }
        }

        public ChangeableObservableCollection<ContentInstallationEntry> DownloadList { get; }

        private bool _hasLoadingItems;

        public bool HasLoadingItems {
            get => _hasLoadingItems;
            set {
                if (Equals(value, _hasLoadingItems)) return;
                _hasLoadingItems = value;
                OnPropertyChanged();
            }
        }

        private int _unfinishedItemsCount;

        public int UnfinishedItemsCount {
            get => _unfinishedItemsCount;
            set {
                if (Equals(value, _unfinishedItemsCount)) return;
                _unfinishedItemsCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnfinishedItems));
            }
        }

        public bool HasUnfinishedItems => _unfinishedItemsCount > 0;

        public void UpdateBusyStates() {
            var hasLoadingItems = false;
            var unfinishedItems = 0;
            foreach (var entry in DownloadList) {
                hasLoadingItems |= entry.State == ContentInstallationEntryState.Loading;
                if (entry.State != ContentInstallationEntryState.Finished) {
                    unfinishedItems++;
                }
            }

            HasLoadingItems = hasLoadingItems;
            UnfinishedItemsCount = unfinishedItems;
        }

        /*public void Cancel() {
            foreach (var entry in DownloadList.ToList()) {
                entry.CancelCommand.Execute();
            }
        }*/

        public event EventHandler TaskAdded;

        private readonly TaskCache _taskCache = new TaskCache();

        public Task<bool> InstallAsync([NotNull] ContentInstallationEntry entry) {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            Logging.Debug(entry.Source);
            return _taskCache.Get(() => ActionExtension.InvokeInMainThread(() => {
                TaskAdded?.Invoke(this, EventArgs.Empty);
                DownloadList.Add(entry);
                return entry.RunAsync();
            }), entry.Source);
        }

        public Task<bool> InstallAsync([NotNull] string source, ContentInstallationParams installationParams = null) {
            return InstallAsync(new ContentInstallationEntry(source, installationParams));
        }

        public static bool IsRemoteSource(string source) {
            return source.StartsWith(@"http:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"https:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"ftp:", StringComparison.OrdinalIgnoreCase);
        }

        [ItemCanBeNull]
        public static async Task<string> IsRemoteSourceFlexible(string url) {
            // TODO: Fix, change HEAD to GET?
            if (!Regex.IsMatch(url, @"^(?:[\w-]+\.)*[\w-]+\.[\w-]+/.+$")) return null;

            try {
                url = new UriBuilder(url).ToString();
                using (var killer = KillerOrder.Create(WebRequest.Create(url) as HttpWebRequest, TimeSpan.FromSeconds(0.5))) {
                    var request = killer.Victim;
                    request.Method = "HEAD";
                    using (var response = await request.GetResponseAsync()) {
                        return (response as HttpWebResponse)?.StatusCode == HttpStatusCode.OK ? url.Replace(@":80/", @"/") : null;
                    }
                }
            } catch (Exception) {
                return null;
            }
        }

        public static bool IsAdditionalContent(string filename) {
            // TODO: or PP-filter, or …?
            try {
                if (!FileUtils.ArePathsEqual(FileUtils.EnsureFilenameIsValid(filename), filename)) return false;
                return FileUtils.Exists(filename) && FileUtils.IsDirectory(filename) ||
                        !filename.EndsWith(@".kn5") && !filename.EndsWith(@".acreplay") && !FileUtils.Affects(AcPaths.GetReplaysDirectory(), filename);
            } catch (Exception e) {
                Logging.Warning(e);
                return false;
            }
        }

        private Busy _busy = new Busy();
        private bool _isDirty;

        public void Save() {
            if (!OptionSaveAndRestoreDownloads) return;
            _isDirty = true;
            _busy.DoDelay(SaveInner, 300);
        }

        public void ForceSave() {
            SaveInner();
        }

        private IEnumerable<ContentInstallationEntry> LoadEntries() {
            return CacheStorage.GetStringList(StorageKey).Select(ContentInstallationEntry.Deserialize).NonNull();
        }

        private void SaveInner() {
            if (!_isDirty || !OptionSaveAndRestoreDownloads) return;
            _isDirty = false;

            var l = DownloadList;
            var sb = new StringBuilder(l.Count * 2);
            for (var i = 0; i < l.Count; i++) {
                var x = l[i];
                if (x.IsDeleted || x.IsDeleting) return;

                if (i > 0) sb.Append('\n');
                sb.Append(Storage.Encode(x.Serialize()));
            }

            CacheStorage.Set(StorageKey, sb.ToString());
        }
    }
}