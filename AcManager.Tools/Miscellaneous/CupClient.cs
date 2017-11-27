using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Miscellaneous {
    public enum CupContentType {
        Car, Track
    }

    public class CupClient {
        private static ApiCacheThing _cache;
        private static ApiCacheThing Cache => _cache ?? (_cache = new ApiCacheThing("CUP", TimeSpan.FromHours(1)));

        public static CupClient Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new CupClient();
        }

        private CupClient() {
            // CheckRepositoriesAsync();
        }

        private async Task CheckRepositoriesAsync() {
            foreach (var registry in SettingsHolder.Content.CupRegistries.Split(new[]{ ';', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                try {
                    await LoadList(registry);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t check registry for updates", e);
                }
            }
        }

        private void RegisterLatestVersion(CupContentType type, [NotNull] string id, [NotNull] string version, bool isLimited) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (version == null) throw new ArgumentNullException(nameof(version));

            Logging.Debug($"Latest version: {type}/{id}: {version} (isLimited={isLimited})");
        }

        private async Task LoadList(string registry) {
            var list = JObject.Parse(await Cache.GetStringAsync(registry + "/list"));
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
                    if (item.Value.Type == JTokenType.String) {
                        RegisterLatestVersion(type, item.Key, (string)item.Value, false);
                    } else if (item.Value is JObject details) {
                        try {
                            RegisterLatestVersion(type, item.Key, (string)details["version"], (bool)details["limited"]);
                        } catch (Exception e) {
                            Logging.Warning("Not supported format: " + type + "/" + item.Key);
                            Logging.Warning(e);
                        }
                    } else {
                        Logging.Warning("Not supported format: " + type + "/" + item.Key);
                    }
                }
            }
        }
    }
}