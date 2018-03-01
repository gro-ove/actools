using System;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class FaviconProvider {
        [ItemCanBeNull]
        public static Task<string> GetFaviconAsync([CanBeNull] string url) {
            return Task.FromResult($@"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(url.GetDomainNameFromUrl()?.ToLowerInvariant() ?? "")}");
        }
    }
}