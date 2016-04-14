using System;
using System.Net;

namespace AcManager.Tools.Helpers {
    public class CookieAwareWebClient : WebClient {
        private readonly CookieContainer _container = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address) {
            var request = base.GetWebRequest(address);
            var webRequest = request as HttpWebRequest;

            if (webRequest != null) {
                webRequest.CookieContainer = _container;
            }

            return request;
        }
    }
}