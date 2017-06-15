using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AcClubLoader : RedirectingLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?assettocorsa\.club/mods/(?:auto|tracks)/", RegexOptions.IgnoreCase);

        public AcClubLoader(string url) : base(url) { }

        protected override async Task<string> GetRedirect(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            var downloadPage = await client.DownloadStringTaskAsync(url);
            if (cancellation.IsCancellationRequested) return null;

            var versionMatch = Regex.Match(downloadPage, @"""spec"">[\s\S]+?V\. ([^<]+)");
            OverrideVersion = versionMatch.Success ? versionMatch.Groups[1].Value : null;

            var downloadUrlMatch = Regex.Match(downloadPage, @"<p class=""download""><a href=""([^""]+)");
            if (!downloadUrlMatch.Success) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_AcClubChanged);
                return null;
            }

            return HttpUtility.HtmlDecode(downloadUrlMatch.Groups[1].Value);
        }
    }
}