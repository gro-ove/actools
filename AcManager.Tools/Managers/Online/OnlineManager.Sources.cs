using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class OnlineManager {
        private static readonly Dictionary<string, IOnlineSource> Sources = new Dictionary<string, IOnlineSource>(10);
        private static readonly Dictionary<string, OnlineSourceWrapper> WrappedSources = new Dictionary<string, OnlineSourceWrapper>(10);

        public static void Register(IOnlineSource source) {
            if (Sources.ContainsKey(source.Id)) {
                Logging.Warning($"Source “{source.Id}” already registered");
                return;
            }

            Logging.Warning($"Registering “{source.Id}”: {source.GetType().Name}");
            Sources[source.Id] = source;
        }

        public static void Unregister(ThirdPartyOnlineSource source) {
            if (Sources.Remove(source.Id)) {
                WrappedSources.Remove(source.Id);
            }
        }

        static OnlineManager() {
            Register(KunosOnlineSource.Instance);
            Register(LanOnlineSource.Instance);
            // Register(MinoratingOnlineSource.Instance);

            ThirdPartyOnlineSourcesManager.Instance.Initialize();
            FileBasedOnlineSources.Instance.Initialize();
            Register(FileBasedOnlineSources.RecentInstance);
            Register(FileBasedOnlineSources.FavouritesInstance);
        }

        public static void EnsureInitialized() {}

        public IEnumerable<OnlineSourceWrapper> Wrappers => WrappedSources.Values;

        [CanBeNull]
        private static IOnlineSource GetSource(string key) {
            return Sources.GetValueOrDefault(key);
        }

        [CanBeNull]
        private OnlineSourceWrapper GetWrappedSource(string key) {
            if (WrappedSources.TryGetValue(key, out var result)) return result;

            var source = GetSource(key) ?? FileBasedOnlineSources.Instance.GetSource(key);
            if (source == null) return null;
            
            result = new OnlineSourceWrapper(List, source);
            WrappedSources[key] = result;
            return result;
        }

        public SourcesPack GetSourcesPack([CanBeNull] params string[] keys) {
            if (keys == null) {
                return new SourcesPack(new [] { KunosOnlineSource.Key }
                        .Concat(ThirdPartyOnlineSourcesManager.Instance.List.Select(x => x.Id))
                        .Select(GetWrappedSource).NonNull());
            }
            
            if (keys.Length == 0 || keys.Length == 1 && keys[0] == null) {
                return GetSourcesPack(KunosOnlineSource.Key);
            }

            if (keys.ArrayContains(@"*") || keys.ArrayContains(@"all")) {
                var enumerable = Sources.Keys.Union(FileBasedOnlineSources.Instance.GetSourceKeys());

                if (!SettingsHolder.Online.IntegrateMinorating) {
                    // Because Minorating has the same servers, excluding it from the list
                    enumerable = enumerable.ApartFrom(MinoratingOnlineSource.Key);
                }

                return new SourcesPack(enumerable.Select(GetWrappedSource));
            }

            return new SourcesPack(keys.Select(GetWrappedSource).NonNull());
        }
    }
}