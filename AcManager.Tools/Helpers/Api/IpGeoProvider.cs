using System;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    public static class IpGeoProvider {
        private const string RequestUri = "http://ipinfo.io/geo";

        private static readonly LazierCached<IpGeoEntry> Cached = LazierCached.CreateAsync(@".IpGeoInformation", GetAsyncFn, TimeSpan.FromDays(1));

        private static async Task<IpGeoEntry> GetAsyncFn() {
            using (var killer = KillerOrder.Create(new CookieAwareWebClient(), 10000)) {
                return JsonConvert.DeserializeObject<IpGeoEntry>(await killer.Victim.DownloadStringTaskAsync(RequestUri));
            }
        }

        [ItemCanBeNull]
        public static Task<IpGeoEntry> GetAsync() {
            return Cached.GetValueAsync();
        }
    }
}