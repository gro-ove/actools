using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api.TheSetupMarket {
    public static class TheSetupMarketApiProvider {
        public static bool OptionResurrect = false;

        private static ApiCacheThing _cache;
        private static ApiCacheThing Cache => _cache ?? (_cache = new ApiCacheThing("The Setup Market", TimeSpan.FromHours(24)));

        [ItemCanBeNull]
        public static async Task<string> GetSetup(string setupId, CancellationToken cancellation = default) {
            try {
                return await Cache.GetStringAsync($"http://thesetupmarket.com/api/get-setup-file-details/{setupId}", $"{setupId}.ini",
                        SettingsHolder.Integrated.TheSetupMarketCacheDataPeriod.TimeSpan).ConfigureAwait(false);
            } catch (Exception e) {
                if (!cancellation.IsCancellationRequested) {
                    Logging.Warning(e);
                }

                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<RemoteSetupInformation> GetSetupInformation(string setupId, CancellationToken cancellation = default) {
            try {
                var data = await Cache.GetStringAsync($"http://thesetupmarket.com/api/get-setup/{setupId}", $"{setupId}.json",
                        SettingsHolder.Integrated.TheSetupMarketCacheDataPeriod.TimeSpan).ConfigureAwait(false);
                if (cancellation.IsCancellationRequested) return null;
                return RemoteSetupInformation.FromTheSetupMarketJToken(JObject.Parse(data));
            } catch (Exception e) {
                if (!cancellation.IsCancellationRequested) {
                    Logging.Warning(e);
                }

                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<Tuple<RemoteSetupInformation, string>> GetSetupFullInformation(string setupId,
                CancellationToken cancellation = default) {
            try {
                var ini = await Cache.GetStringAsync($"http://thesetupmarket.com/api/get-setup-file-details/{setupId}", $"{setupId}.ini",
                        SettingsHolder.Integrated.TheSetupMarketCacheDataPeriod.TimeSpan).ConfigureAwait(false);
                if (cancellation.IsCancellationRequested) return null;

                var data = await Cache.GetStringAsync($"http://thesetupmarket.com/api/get-setup/{setupId}", $"{setupId}.json",
                        SettingsHolder.Integrated.TheSetupMarketCacheDataPeriod.TimeSpan).ConfigureAwait(false);
                if (cancellation.IsCancellationRequested) return null;

                return Tuple.Create(RemoteSetupInformation.FromTheSetupMarketJToken(JObject.Parse(data)), ini);
            } catch (Exception e) {
                if (!cancellation.IsCancellationRequested) {
                    Logging.Warning(e);
                }

                return null;
            }
        }

        private static List<RemoteSetupInformation> _parsed;
        private static DateTime _parsedLifeSpan;
        private static DateTime? _errorSkip;

        private static string[] ListUrls = {
            "http://193.176.79.190:12012/setups",
            // "http://thesetupmarketcache-x4fab.rhcloud.com/setups",
            // "http://thesetupmarket.com/api/get-setups/Assetto%20Corsa"
        };

        private static readonly TaskCache TaskCache = new TaskCache();

        [ItemCanBeNull]
        public static Task<List<RemoteSetupInformation>> GetAvailableSetups(string carId, CancellationToken cancellation = default) {
            return TaskCache.Get(() => GetAvailableSetupsInner(carId, cancellation), nameof(GetAvailableSetupsInner), carId);
        }

        public static async Task<List<RemoteSetupInformation>> GetAvailableSetupsInner(string carId, CancellationToken cancellation = default) {
            var now = DateTime.Now;
            if (_errorSkip.HasValue && now < _errorSkip.Value) {
                return null;
            }

            if (!OptionResurrect) {
                return null;
            }

            if (_parsed != null && now < _parsedLifeSpan) {
                // A little bit of extra caching to avoid going to a different thread to reparse already loaded data
                // for each new car.
                return _parsed.Where(x => x.CarId == carId).ToList();
            }

            try {
                foreach (var url in SettingsHolder.Integrated.TheSetupMarketCacheServer ? ListUrls : new[]{ ListUrls.Last() }) {
                    Logging.Write(url);

                    try {
                        var data = await Cache.GetStringAsync(url, "List.json",
                                SettingsHolder.Integrated.TheSetupMarketCacheListPeriod.TimeSpan).ConfigureAwait(false);
                        _parsed = await Task.Run(() => JArray.Parse(data).Select(x =>
                                RemoteSetupInformation.FromTheSetupMarketJToken(x)).NonNull().ToList());
                        if (cancellation.IsCancellationRequested || data == null) return null;
                        break;
                    } catch (Exception e) {
                        Logging.Warning($"Error while loading {url}: {e}");
                        Cache.ResetCache("List.json");
                        if (url == ListUrls.Last()) {
                            Logging.Warning("No more URLs to try");
                            throw;
                        }
                    }
                }

                if (_parsed == null) {
                    throw new Exception("Failed to load any data");
                }

                _parsedLifeSpan = DateTime.Now + TimeSpan.FromHours(3);
                return _parsed.Where(x => x.CarId == carId).ToList();
            } catch (Exception e) {
                if (!cancellation.IsCancellationRequested) {
                    Logging.Warning(e);
                }

                _errorSkip = DateTime.Now + TimeSpan.FromHours(3);
                return null;
            }
        }
    }
}