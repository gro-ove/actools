using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public abstract class JsBridgeBase {
        // Do not include “www.” here!
        public Collection<string> AcApiHosts { get; } = new Collection<string>();

        public bool IsHostAllowed(string url) {
            var domain = url.GetDomainNameFromUrl();
            return AcApiHosts.Contains(domain, StringComparer.OrdinalIgnoreCase)
                    || AcApiHosts.Any(x => x.StartsWith(".") && domain.EndsWith(x, StringComparison.OrdinalIgnoreCase));
        }

        public WebTab Tab { get; set; }

        public virtual string AcApiRequest(string url) {
            return null;
        }

        public virtual void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) { }

        public virtual void PageHeaders(string url, IDictionary<string, string> headers) { }

        public virtual void PageLoaded(string url) { }

        [CanBeNull]
        protected abstract JsProxyBase MakeProxy();

        public JsProxyBase Proxy => _proxy ?? (_proxy = MakeProxy());

        private JsProxyBase _proxy;
    }
}