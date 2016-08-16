using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Api {
    public static class YahooApiProvider {
        private const string RequestLocationUri = "https://query.yahooapis.com/v1/public/yql?q=select centroid from geo.places(1) where text='{0}'";
        private static readonly Regex CleanUpRegex = new Regex(@"[^,\w-]+", RegexOptions.Compiled);

        [ItemNotNull]
        public static async Task<GeoTagsEntry> LocateAsync([CanBeNull] string address) {
            var requestUri = string.Format(RequestLocationUri, HttpUtility.UrlEncode(CleanUpRegex.Replace(address ?? "", " ")));
            using (var order = KillerOrder.Create(new WebClient(), 5000)) {
                var data = await order.Victim.DownloadStringTaskAsync(requestUri);
                var ns = XNamespace.Get(@"http://where.yahooapis.com/v1/schema.rng");
                var centroid = XDocument.Parse(data).Descendants(ns + @"centroid").FirstOrDefault();
                var latitude = centroid?.Element(ns + @"latitude");
                var longitude = centroid?.Element(ns + @"longitude");
                if (latitude == null || longitude == null) throw new Exception("Invalid response");
                return new GeoTagsEntry(FlexibleParser.ParseDouble(latitude.Value),
                        FlexibleParser.ParseDouble(longitude.Value));
            }
        }
    }
}
