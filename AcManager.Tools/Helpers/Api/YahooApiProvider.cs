using System;
using System.Globalization;
using System.Net;
using System.Web;
using System.Xml;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Api {
    public class YahooApiProvider {
        private const string RequestLocationUri = "https://query.yahooapis.com/v1/public/yql?q=" +
            "select centroid from geo.places(1) where text='{1},{0}'";

        public GeoTagsEntry TryToLocate(string country, string city) {
            var requestUri = string.Format(RequestLocationUri, HttpUtility.UrlEncode(country), HttpUtility.UrlEncode(city));

            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";

                using (var response = (HttpWebResponse) httpRequest.GetResponse()) {
                    if (response.StatusCode != HttpStatusCode.OK) return null;

                    using (var responseStream = response.GetResponseStream()) {
                        if (responseStream == null) return null;

                        var xml = new XmlDocument();
                        xml.Load(responseStream);
                        
                        var centroid = xml.GetElementsByTagName("centroid")[0];
                        return new GeoTagsEntry(
                            double.Parse(centroid["latitude"].InnerText, CultureInfo.InvariantCulture),
                            double.Parse(centroid["longitude"].InnerText, CultureInfo.InvariantCulture)
                        );
                    }
                }
            } catch (Exception e) {
                Logging.Warning("cannot locate city using yahoo: {0}\n{1}", requestUri, e);
                return null;
            }
        }
    }
}
