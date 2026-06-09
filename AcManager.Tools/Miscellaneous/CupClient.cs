using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Miscellaneous {
    public class CupClient {
        private static ApiCacheThing _cache;
        private static ApiCacheThing Cache => _cache ?? (_cache = new ApiCacheThing("CUP", TimeSpan.FromHours(1), true));

        [CanBeNull]
        public static CupClient Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new CupClient();
        }

        private readonly Storage _storage;

        private CupClient() {
            _storage = new Storage(FilesStorage.Instance.GetFilename("Progress", "CUP.data"));
        }

        private readonly Dictionary<CupContentType, IAcManagerNew> _managers = new Dictionary<CupContentType, IAcManagerNew>();
        private readonly Dictionary<CupContentType, Func<IAcManagerNew>> _postponed = new Dictionary<CupContentType, Func<IAcManagerNew>>();

        [CanBeNull]
        public IAcManagerNew GetAssociatedManager(CupContentType type, bool createMissing) {
            if (_managers.TryGetValue(type, out var result)) return result;
            if (createMissing && _postponed.TryGetValue(type, out var factory)) return factory();
            return null;
        }

        public static void Register<T>(BaseAcManager<T> manager, CupContentType type) where T : AcObjectNew, ICupSupportedObject {
            // Logging.Debug($"REGISTERING : {type}, {manager}, {Instance}");
            if (Instance == null) {
#if DEBUG
                throw new Exception("Nowhere to register: " + manager);
#endif
                return;
            }
            Instance._managers.Add(type, manager);
            Instance.NewLatestVersion += (sender, args) => {
                if (args.Key.Type != type) return;
                var obj = manager.GetWrapperById(args.Key.Id);
                if (obj == null || !obj.IsLoaded) return;
                ((ICupSupportedObject)obj.Value).OnCupUpdateAvailableChanged();
            };
        }

        private int _postponedDelay = 3;

        public void RegisterPostponed<T>(CupContentType type, Func<BaseAcManager<T>> factory) where T : AcObjectNew, ICupSupportedObject {
            var postponed = new List<CupEventArgs>();
            EventHandler<CupEventArgs> h = (sender, args) => {
                if (args.Key.Type != type) return;
                postponed.Add(args);
            };
            _postponed.Add(type, factory);
            NewLatestVersion += h;
            Task.Delay(TimeSpan.FromSeconds(++_postponedDelay)).ContinueWithInMainThread(r => {
                var manager = factory();
                foreach (var args in postponed) {
                    var obj = manager.GetWrapperById(args.Key.Id);
                    if (obj == null || !obj.IsLoaded) return;
                    ((ICupSupportedObject)obj.Value).OnCupUpdateAvailableChanged();
                }
                _postponed.Remove(type);
                NewLatestVersion -= h;
            });
        }

        public bool IsPostponed(CupContentType type) {
            return _postponed.ContainsKey(type);
        }

        private readonly TaskCache _loadCache = new TaskCache();

        public Task LoadRegistries(bool clearCache = false) {
            if (clearCache) {
                _cache.Clear();
            }

            return _loadCache.Get(CheckRegistriesAsync);
        }

        private async Task CheckRegistriesAsync() {
            foreach (var registry in SettingsHolder.Content.CupRegistriesList) {
                try {
                    await LoadList(registry.ApartFromLast(@"/"));
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t check registry for content updates", e);
                }
            }
        }

        public class CupKey {
            public readonly CupContentType Type;

            [NotNull]
            public readonly string Id;

            public CupKey(CupContentType type, [NotNull] string id) {
                Type = type;
                Id = (id ?? throw new ArgumentNullException(nameof(id))).ToLowerInvariant();
            }

            protected bool Equals(CupKey other) {
                return Type == other.Type && string.Equals(Id, other.Id);
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.GetType() == GetType() && Equals((CupKey)obj);
            }

            public override int GetHashCode() {
                unchecked {
                    return ((int)Type * 397) ^ Id.GetHashCode();
                }
            }

            public override string ToString() {
                return $@"{Type}/{Id}";
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class CupInformation : NotifyPropertyChanged {
            public bool IsToUpdateManually { get; }

            internal string SourceRegistry;
            internal bool IsExtendedLoaded;

            #region Extra properties
            [CanBeNull, JsonProperty("name")]
            public string Name { get; private set; }

            [CanBeNull, JsonProperty("version")]
            public string Version { get; private set; }

            [CanBeNull, JsonProperty("author")]
            public string Author { get; private set; }

            [CanBeNull, JsonProperty("changelog")]
            public string Changelog { get; private set; }

            [JsonProperty("cleanInstallation")]
            public bool PreferCleanInstallation { get; private set; }

            [CanBeNull, JsonProperty("informationUrl")]
            public string InformationUrl { get; private set; }

            [CanBeNull, JsonProperty("alternativeIds")]
            public string[] AlternativeIds { get; private set; }

            [CanBeNull, JsonIgnore]
            // ReSharper disable once InconsistentNaming
            internal Tuple<string, string> _updateUrlAndName { get; set; }
            #endregion

            [JsonConstructor]
            private CupInformation(bool limited) {
                IsToUpdateManually = limited;
            }

            public CupInformation([NotNull] string version, bool limited, string sourceRegistry) {
                Version = version ?? throw new ArgumentNullException(nameof(version));
                IsToUpdateManually = limited;
                SourceRegistry = sourceRegistry.ApartFromLast("/");
            }

            public override string ToString() {
                return $@"(version={Version}; limited={IsToUpdateManually})";
            }
        }

        public bool ContainsAnUpdate(CupContentType type, [NotNull] string id, [CanBeNull] string installedVersion) {
            return _versions.TryGetValue(new CupKey(type, id), out var information) && information.Version.IsVersionNewerThan(installedVersion);
        }

        public async Task<CupInformation> LoadCupEntryAsync(string registry, CupKey key) {
            var data = JsonConvert.DeserializeObject<CupInformation>(
                    await Cache.GetStringAsync($@"{registry}/{key.Type.CupID()}/{CmIDToCupContentID(key.Type, key.Id)}"));
            data.SourceRegistry = registry.ApartFromLast("/");
            data.IsExtendedLoaded = true;
            RegisterLatestVersion(key, data);
            return data;
        }

        [CanBeNull]
        public CupInformation GetInformation(CupContentType type, [NotNull] string id) {
            var key = new CupKey(type, id);
            if (!_versions.TryGetValue(key, out var data)) return null;

            if (!data.IsExtendedLoaded) {
                data.IsExtendedLoaded = true;
                LoadCupEntryAsync(data.SourceRegistry, key).ContinueWith(r => {
                    if (r.IsFaulted) {
                        NonfatalError.NotifyBackground("Failed to load CUP entry", r.Exception);
                    }
                });
            }

            return data;
        }

        private readonly TaskCache _installationUrlTaskCache = new TaskCache();

        public Task<Tuple<string, string>> GetUpdateUrlFromAsync(string registry, CupContentType type, [NotNull] string id) {
            return _installationUrlTaskCache.Get(async () => {
                try {
                    var url = $@"{registry}/{type.CupID()}/{CmIDToCupContentID(type, id)}/get";
                    using (var client = new CookieAwareWebClient()) {
                        string fileName = null;
                        var ret = await client.GetFinalRedirectAsync(url, 1, s => fileName = s);
                        return Tuple.Create(ret, fileName);
                    }
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t download an update", e);
                    return null;
                }
            }, registry, type, id);
        }

        [ItemCanBeNull]
        public async Task<Tuple<string, string>> GetUpdateUrlAsync(CupContentType type, [NotNull] string id) {
            var information = GetInformation(type, id);
            if (information == null || information._updateUrlAndName != null) return information?._updateUrlAndName;

            var ret = await GetUpdateUrlFromAsync(information.SourceRegistry, type, id);
            information._updateUrlAndName = ret;
            return ret;
        }

        private readonly TaskCache _reportTaskCache = new TaskCache();

        public Task<bool> ReportUpdateAsync(CupContentType type, [NotNull] string id) {
            var information = GetInformation(type, id);
            if (information == null) return Task.FromResult(false);

            return _reportTaskCache.Get(async () => {
                try {
                    var url = $@"{information.SourceRegistry}/{type.CupID()}/{CmIDToCupContentID(type, id)}/complain";
                    using (var client = new CookieAwareWebClient()) {
                        await client.UploadStringTaskAsync(url, "");
                        IgnoreUpdate(type, id);
                        Toast.Show("Update reported", "Update reported and will be ignored. Thank you for your contribution");
                        return true;
                    }
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t report an update", e);
                    return false;
                }
            }, type, id);
        }

        private readonly TaskCache _installTaskCache = new TaskCache();

        public Task<bool> InstallUpdateAsync(CupContentType type, [NotNull] string id) {
            var information = GetInformation(type, id);
            if (information == null) {
                Logging.Warning($"Failed to update: information == null (type={type}, ID={id})");
                return Task.FromResult(false);
            }

            return _installTaskCache.Get(async () => {
                var updateUrl = await GetUpdateUrlAsync(type, id);
                Logging.Debug(updateUrl);

                if (information.IsToUpdateManually) {
                    WindowsHelper.ViewInBrowser(updateUrl?.Item1 ?? information.InformationUrl);
                    return false;
                }

                var idsToUpdate = new[] { id }.Append(information.AlternativeIds ?? new string[0]).ToArray();
                return updateUrl != null && await ContentInstallationManager.Instance.InstallAsync(updateUrl.Item1,
                        new ContentInstallationParams(true) {
                            // Actual installation params
                            PreferCleanInstallation = information.PreferCleanInstallation,

                            // For displaying stuff
                            ForcedFileName = updateUrl.Item2,
                            DisplayName = information.Name,
                            IdsToUpdate = idsToUpdate,

                            // For updating content
                            CupType = type,
                            Author = information.Author,
                            Version = information.Version,
                            InformationUrl = information.InformationUrl,
                            SyncDetails = true
                        });
            }, type, id);
        }

        private ICupSupportedObject GetLoaded(CupContentType type, [NotNull] string id) {
            return GetAssociatedManager(type, true)?.WrappersAsIList.OfType<AcItemWrapper>()
                    .GetByIdOrDefault(id, StringComparison.InvariantCultureIgnoreCase)?.Value
                    as ICupSupportedObject;
        }

        public void IgnoreUpdate(CupContentType type, [NotNull] string id) {
            var information = GetInformation(type, id);
            if (information == null) return;
            _storage.Set($"ignore:{new CupKey(type, id)}:{information.Version}", true);
            _versions.Remove(new CupKey(type, id));
            GetLoaded(type, id).OnCupUpdateAvailableChanged();
        }

        public void IgnoreAllUpdates(CupContentType type, [NotNull] string id) {
            _storage.Set($"ignore:{new CupKey(type, id)}", true);
            _versions.Remove(new CupKey(type, id));
            GetLoaded(type, id).OnCupUpdateAvailableChanged();
        }

        private readonly Dictionary<CupKey, CupInformation> _versions = new Dictionary<CupKey, CupInformation>();

        public IReadOnlyDictionary<CupKey, CupInformation> List => _versions;

        public event EventHandler<CupEventArgs> NewLatestVersion;

        private void RegisterLatestVersion([NotNull] CupKey key, [NotNull] CupInformation information) {
            if (_storage.Get<bool>($"ignore:{key}") || _storage.Get<bool>($"ignore:{key}:{information.Version}") ||
                    _versions.TryGetValue(key, out var existing) && existing.Version.IsVersionNewerThan(information.Version)) {
                return;
            }

            _versions[key] = information;
            NewLatestVersion?.Invoke(this, new CupEventArgs(key, information));
        }

        private static string CupContentIDToCmID(CupContentType type, string id) {
            // TODO: Do this one better
            if (type == CupContentType.Filter) return id + @".ini";
            return id;
        }

        private static string CmIDToCupContentID(CupContentType type, string id) {
            // TODO: Do this one better
            if (type == CupContentType.Filter) return id.ApartFromLast(@".ini");
            return id;
        }

        private async Task LoadList(string registry) {
            var data = await Cache.GetStringAsync(registry);
            JObject list;
            try {
                list = JObject.Parse(data);
            } catch {
                Logging.Warning($"Failed to parse: {data}");
                return;
            }
            foreach (var p in list) {
                if (!Enum.TryParse<CupContentType>(p.Key, true, out var type)) {
                    Logging.Warning("Not supported type: " + type);
                    continue;
                }

                if (!(p.Value is JObject obj)) {
                    Logging.Warning("Not supported format: " + type);
                    continue;
                }

                foreach (var item in obj) {
                    try {
                        var key = new CupKey(type, CupContentIDToCmID(type, item.Key));
                        if (item.Value.Type == JTokenType.String) {
                            RegisterLatestVersion(key, new CupInformation((string)item.Value, false, registry));
                        } else if (item.Value is JObject details) {
                            RegisterLatestVersion(key, new CupInformation((string)details["version"], (bool)details["limited"], registry));
                        } else {
                            Logging.Warning("Not supported format: " + type + "/" + item.Key);
                        }
                    } catch (Exception e) {
                        Logging.Warning("Not supported format: " + type + "/" + item.Key);
                        Logging.Warning(e);
                    }
                }
            }
        }
    }
}