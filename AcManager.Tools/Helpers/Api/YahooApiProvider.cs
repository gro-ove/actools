using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Api {
    public static class YahooApiProvider {
        private const string RequestLocationUri = "https://query.yahooapis.com/v1/public/yql?q=" +
            "select centroid from geo.places(1) where text='{0}'";

        private static readonly Regex CleanUpRegex = new Regex(@"[^,\w-]+", RegexOptions.Compiled);

        [CanBeNull]
        public static GeoTagsEntry TryToLocate([CanBeNull] string address) {
            var requestUri = string.Format(RequestLocationUri, HttpUtility.UrlEncode(CleanUpRegex.Replace(address ?? "", " ")));

            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";

                using (var response = (HttpWebResponse)httpRequest.GetResponse()) {
                    if (response.StatusCode != HttpStatusCode.OK) return null;

                    using (var responseStream = response.GetResponseStream()) {
                        if (responseStream == null) return null;

                        var xml = new XmlDocument();
                        xml.Load(responseStream);

                        var centroid = xml.GetElementsByTagName("centroid")[0];
                        var latitude = centroid?["latitude"];
                        var longitude = centroid?["longitude"];
                        return latitude == null || longitude == null ? null : new GeoTagsEntry(latitude.InnerText.AsDouble(),
                                longitude.InnerText.AsDouble());
                    }
                }
            } catch (Exception e) {
                Logging.Warning($"Cannot locate city using Yahoo: {requestUri}\n{e}");
                return null;
            }
        }
    }
}
