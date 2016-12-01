using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class OnlineManager {
        private static readonly Dictionary<string, IOnlineSource> Sources = new Dictionary<string, IOnlineSource>(10);

        public static void Register(IOnlineSource source) {
            if (Sources.ContainsKey(source.Key)) {
                Logging.Warning($"Source “{source.Key}” already registered");
                return;
            }

            Sources[source.Key] = source;
        }

        static OnlineManager() {
            Register(KunosOnlineSource.Instance);
            Register(LanOnlineSource.Instance);
            Register(MinoratingOnlineSource.Instance);

            FileBasedOnlineSources.Instance.Initialize();
            Register(FileBasedOnlineSources.RecentInstance);
            Register(FileBasedOnlineSources.FavoritesInstance);
        }

        private readonly Dictionary<string, OnlineSourceWrapper> _wrappers = new Dictionary<string, OnlineSourceWrapper>(10);

        [CanBeNull]
        private IOnlineSource GetSource(string key) {
            return Sources.GetValueOrDefault(key);
        }

        [CanBeNull]
        private OnlineSourceWrapper GetWrappedSource(string key) {
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

            if (keys.Contains(@"*")) {
                return new SourcesPack(Sources.Keys.Select(GetWrappedSource));
            }

            return new SourcesPack(keys.Select(GetWrappedSource).NonNull());
        }
    }
}