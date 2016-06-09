using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Api {
    public static class YahooApiProvider {
        private const string RequestLocationUri = "https://query.yahooapis.com/v1/public/yql?q=" +
            "select centroid from geo.places(1) where text='{1},{0}'";

        private static readonly Regex CleanUpRegex = new Regex(@"[^\w-]+", RegexOptions.Compiled);

        [CanBeNull]
        public static GeoTagsEntry TryToLocate([CanBeNull] string country, [CanBeNull] string city) {
            country = CleanUpRegex.Replace(country ?? "", " ").Trim();
            city = CleanUpRegex.Replace(city ?? "", " ").Trim();

            var requestUri = string.Format(RequestLocationUri, HttpUtility.UrlEncode(country), HttpUtility.UrlEncode(city));

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
                        if (centroid == null) return null;

                        var latitude = centroid["latitude"];
                        var longitude = centroid["longitude"];
                        if (latitude == null || longitude == null) return null;

                        return new GeoTagsEntry(
                                double.Parse(latitude.InnerText, CultureInfo.InvariantCulture),
                                double.Parse(longitude.InnerText, CultureInfo.InvariantCulture));
                    }
                }
            } catch (Exception e) {
                Logging.Warning("cannot locate city using yahoo: {0}\n{1}", requestUri, e);
                return null;
            }
        }
    }
}
