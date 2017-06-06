using System;
using System.Net;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers {
    public class CookieAwareWebClient : WebClient {
        private readonly CookieContainer _container = new CookieContainer();

        private string _method;

        public IDisposable SetMethod(string method) {
            var oldMethod = _method;
            _method = method;
            return new ActionAsDisposable(() => {
                _method = oldMethod;
            });
        }

        protected override WebRequest GetWebRequest(Uri address) {
            var request = base.GetWebRequest(address);
            var webRequest = request as HttpWebRequest;

            if (webRequest != null) {
                webRequest.CookieContainer = _container;

                if (!string.IsNullOrEmpty(_method)) {
                    webRequest.Method = _method;
                }
            }

            return request;
        }
    }
}