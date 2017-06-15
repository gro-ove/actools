using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AssettoDbLoader : RedirectingLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?assetto-db\.com/", RegexOptions.IgnoreCase);
        public static bool DownloadTest(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?assetto-db\.com/.+/download\b", RegexOptions.IgnoreCase);

        public AssettoDbLoader(string url) : base(url) { }

        protected override async Task<string> GetRedirect(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            var downloadUrl = url;

            if (!DownloadTest(downloadUrl)) {
                var itemPage = await client.DownloadStringTaskAsync(url);
                if (cancellation.IsCancellationRequested) return null;

                var downloadUrlMatch = Regex.Match(itemPage, @"href=""(/[^""]+/download)""");
                if (!downloadUrlMatch.Success) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_AssettoDbChanged);
                    return null;
                }

                downloadUrl = "http://assetto-db.com" + HttpUtility.HtmlDecode(downloadUrlMatch.Groups[1].Value);
            }

            var downloadPage = await client.DownloadStringTaskAsync(downloadUrl);
            if (cancellation.IsCancellationRequested) return null;

            var match = Regex.Match(downloadPage, @"\bwindow\.location='([^']+)'");
            if (!match.Success) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_AssettoDbChanged);
                return null;
            }

            return match.Groups[1].Value;
        }
    }
}