using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AcDriftingProLoader : RedirectingLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?acdriftingpro\.com/", RegexOptions.IgnoreCase);

        public AcDriftingProLoader(string url) : base(url) { }

        protected override async Task<string> GetRedirectOverrideAsync(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            var downloadPage = await client.DownloadStringTaskAsync(url);
            if (cancellation.IsCancellationRequested) return null;

            var downloadUrlMatch = Regex.Match(downloadPage, @"\bhref=""(https?://(?:www\.)?acstuff\.ru/s/[^""]+)", RegexOptions.IgnoreCase);
            if (!downloadUrlMatch.Success) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, "ACDriftingPro.com has changed.");
                return null;
            }

            return HttpUtility.HtmlDecode(downloadUrlMatch.Groups[1].Value);
        }
    }
}