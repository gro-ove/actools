using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class OnlineManager {
        private static readonly Dictionary<string, IOnlineSource> Sources = new Dictionary<string, IOnlineSource>(10);

        public static void Register(IOnlineSource source) {
            if (Sources.ContainsKey(source.Id)) {
                Logging.Warning($"Source “{source.Id}” already registered");
                return;
            }

            Sources[source.Id] = source;
        }

        static OnlineManager() {
            Register(KunosOnlineSource.Instance);
            Register(LanOnlineSource.Instance);
            Register(MinoratingOnlineSource.Instance);

            FileBasedOnlineSources.Instance.Initialize();
            Register(FileBasedOnlineSources.RecentInstance);
            Register(FileBasedOnlineSources.FavouritesInstance);
        }

        public static void EnsureInitialized() {}

        private readonly Dictionary<string, OnlineSourceWrapper> _wrappers = new Dictionary<string, OnlineSourceWrapper>(10);

        public IEnumerable<OnlineSourceWrapper> Wrappers => _wrappers.Values;

        [CanBeNull]
        private static IOnlineSource GetSource(string key) {
            return Sources.GetValueOrDefault(key);
        }

        [CanBeNull]
        public OnlineSourceWrapper GetWrappedSource(string key) {
            OnlineSourceWrapper result;
            if (_wrappers.TryGetValue(key, out result)) return result;

            var source = GetSource(key) ?? FileBasedOnlineSources.Instance.GetSource(key);
            result = new OnlineSourceWrapper(List, source);
            _wrappers[key] = result;
            return result;
        }

        public SourcesPack GetSourcesPack([CanBeNull] params string[] keys) {
            if (keys == null || keys.Length == 0 || keys.Length == 1 && keys[0] == null) {
                return GetSourcesPack(KunosOnlineSource.Key);
            }

            if (keys.ArrayContains(@"*") || keys.ArrayContains(@"all")) {
                var enumerable = Sources.Keys.Union(FileBasedOnlineSources.Instance.GetSourceKeys());

                if (!SettingsHolder.Online.IntegrateMinorating) {
                    enumerable = enumerable.ApartFrom(MinoratingOnlineSource.Key);
                }

                return new SourcesPack(enumerable.Select(GetWrappedSource));
            }

            return new SourcesPack(keys.Select(GetWrappedSource).NonNull());
        }
    }
}