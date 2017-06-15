using System;
using System.Net;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

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

        public IDisposable SetProxy([CanBeNull] string proxy) {
            if (string.IsNullOrWhiteSpace(proxy)) return new ActionAsDisposable(() => { });

            var oldValue = Proxy;
            try {
                Proxy = string.IsNullOrWhiteSpace(proxy) ? null : new WebProxy(proxy);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t set proxy", e);
            }

            return new ActionAsDisposable(() => {
                Proxy = oldValue;
            });
        }

        public IDisposable SetUserAgent(string newUserAgent) {
            var oldValue = Headers[HttpRequestHeader.UserAgent];
            Headers[HttpRequestHeader.UserAgent] = newUserAgent;
            return new ActionAsDisposable(() => {
                Headers[HttpRequestHeader.UserAgent] = oldValue;
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