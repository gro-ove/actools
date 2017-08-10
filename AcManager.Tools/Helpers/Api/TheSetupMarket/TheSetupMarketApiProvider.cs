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
        private static ApiCacheThing _cache;
        private static ApiCacheThing Cache => _cache ?? (_cache = new ApiCacheThing("The Setup Market", TimeSpan.FromHours(24)));

        [ItemCanBeNull]
        public static async Task<string> GetSetup(string setupId, CancellationToken cancellation = default(CancellationToken)) {
            try {
                return await Cache.GetAsync($"http://thesetupmarket.com/api/get-setup-file-details/{setupId}", $"{setupId}.ini",
                        SettingsHolder.Integrated.TheSetupMarketCacheDataPeriod.TimeSpan).ConfigureAwait(false);
            } catch (Exception e) {
                if (!cancellation.IsCancellationRequested) {
                    Logging.Warning(e);
                }

                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<RemoteSetupInformation> GetSetupInformation(string setupId, CancellationToken cancellation = default(CancellationToken)) {
            try {
                var data = await Cache.GetAsync($"http://thesetupmarket.com/api/get-setup/{setupId}", $"{setupId}.json",
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
                CancellationToken cancellation = default(CancellationToken)) {
            try {
                var ini = await Cache.GetAsync($"http://thesetupmarket.com/api/get-setup-file-details/{setupId}", $"{setupId}.ini",
                        SettingsHolder.Integrated.TheSetupMarketCacheDataPeriod.TimeSpan).ConfigureAwait(false);
                if (cancellation.IsCancellationRequested) return null;

                var data = await Cache.GetAsync($"http://thesetupmarket.com/api/get-setup/{setupId}", $"{setupId}.json",
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

        [ItemCanBeNull]
        public static async Task<List<RemoteSetupInformation>> GetAvailableSetups(string carId, CancellationToken cancellation = default(CancellationToken)) {
            if (_parsed != null && DateTime.Now < _parsedLifeSpan) {
                // A little bit of extra caching to avoid going to a different thread to reparse already loaded data
                // for each new car.
                return _parsed.Where(x => x.CarId == carId).ToList();
            }

            try {
                var data = await Cache.GetAsync(SettingsHolder.Integrated.TheSetupMarketCacheServer ?
                        "http://thesetupmarketcache-x4fab.rhcloud.com/setups" :
                        "http://thesetupmarket.com/api/get-setups/Assetto%20Corsa", "List.json",
                        SettingsHolder.Integrated.TheSetupMarketCacheListPeriod.TimeSpan).ConfigureAwait(false);
                if (cancellation.IsCancellationRequested || data == null) return null;

                _parsed = await Task.Run(() => JArray.Parse(data).Select(x => RemoteSetupInformation.FromTheSetupMarketJToken(x)).NonNull().ToList());
                _parsedLifeSpan = DateTime.Now + TimeSpan.FromHours(3);
                return _parsed.Where(x => x.CarId == carId).ToList();
            } catch (Exception e) {
                if (!cancellation.IsCancellationRequested) {
                    Logging.Warning(e);
                }

                return null;
            }
        }
    }
}