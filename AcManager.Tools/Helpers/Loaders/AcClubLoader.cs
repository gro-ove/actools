using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AcClubLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?assettocorsa.club/mods/(?:auto|tracks)/", RegexOptions.IgnoreCase);

        public AcClubLoader(string url) : base(url) { }

        private ILoader _innerLoader;

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            var downloadPage = await client.DownloadStringTaskAsync(Url);
            if (cancellation.IsCancellationRequested) return false;

            var versionMatch = Regex.Match(downloadPage, @"""spec"">[\s\S]+?V\. ([^<]+)");
            Version = versionMatch.Success ? versionMatch.Groups[1].Value : null;

            var downloadUrlMatch = Regex.Match(downloadPage, @"<p class=""download""><a href=""([^""]+)");
            if (!downloadUrlMatch.Success) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_AcClubChanged);
                return false;
            }

            Url = HttpUtility.HtmlDecode(downloadUrlMatch.Groups[1].Value);
            Logging.Write("AssettoCorsa.club download link: " + Url);

            _innerLoader = FlexibleLoader.CreateLoader(Url);
            if (_innerLoader is AcClubLoader) throw new Exception(ToolsStrings.DirectLoader_RecursionDetected);
            return await _innerLoader.PrepareAsync(client, cancellation);
        }

        public override long TotalSize => _innerLoader?.TotalSize ?? -1L;
        public override string FileName => _innerLoader?.FileName;

        public override Task DownloadAsync(CookieAwareWebClient client, string destination, CancellationToken cancellation) {
            return _innerLoader.DownloadAsync(client, destination, cancellation);
        }

        public override Task<string> GetDownloadLink(CancellationToken cancellation) {
            return _innerLoader.GetDownloadLink(cancellation);
        }
    }
}