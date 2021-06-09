using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using AcManager.Internal;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api {
    public static class PositionStackApiProvider {
        private const string RequestLocationUri = "http://api.positionstack.com/v1/forward?access_key={0}&limit=1&query={1}";
        private static readonly Regex CleanUpRegex = new Regex(@"[^,\w-]+", RegexOptions.Compiled);

        [ItemNotNull]
        public static async Task<GeoTagsEntry> LocateAsync([CanBeNull] string address) {
            var requestUri = string.Format(RequestLocationUri, InternalUtils.GetPositionStackApiKey().Item1,
                    HttpUtility.UrlEncode(CleanUpRegex.Replace(address ?? "", " ")));
            using (var order = KillerOrder.Create(new WebClient(), 5000)) {
                var data = await order.Victim.DownloadStringTaskAsync(requestUri);
                var item = JObject.Parse(data)[@"data"][0];
                return new GeoTagsEntry(
                        item.GetDoubleValueOnly("latitude") ?? throw new Exception("Invalid response: no latitude"),
                        item.GetDoubleValueOnly("longitude") ?? throw new Exception("Invalid response: no longitude"));
            }
        }
    }
}