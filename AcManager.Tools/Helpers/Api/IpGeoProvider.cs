using System;
using System.Net;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    public static class IpGeoProvider {
        private const string RequestUri = "http://ipinfo.io/geo";

        private static readonly LazierCached<IpGeoEntry> Cached = LazierCached.CreateAsync(@".IpGeoInformation", GetAsyncFn, TimeSpan.FromDays(1));

        private static Task<IpGeoEntry> GetAsyncFn() {
            return Task.Run(() => {
                var httpRequest = WebRequest.Create(RequestUri);
                httpRequest.Method = "GET";
                using (var response = (HttpWebResponse)httpRequest.GetResponse()) {
                    return response.StatusCode != HttpStatusCode.OK
                            ? null : JsonConvert.DeserializeObject<IpGeoEntry>(response.GetResponseStream()?.ReadAsStringAndDispose());
                }
            });
        }

        [ItemCanBeNull]
        public static Task<IpGeoEntry> GetAsync() {
            return Cached.GetValueAsync();
        }
    }
}