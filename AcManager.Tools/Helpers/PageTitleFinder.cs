using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using FirstFloor.ModernUI.Helpers;
using HtmlAgilityPack;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class PageTitleFinder {
        [ItemCanBeNull]
        public static async Task<string> GetPageTitle([CanBeNull] string url) {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try {
                using (var wc = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromSeconds(10))) {
                    var data = await wc.Victim.DownloadStringTaskAsync(url);

                    var doc = new HtmlDocument();
                    doc.LoadHtml(data);

                    return HttpUtility.HtmlDecode(doc.DocumentNode.Descendants(@"title")?.FirstOrDefault()?.InnerText.Trim());
                }
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }
    }
}