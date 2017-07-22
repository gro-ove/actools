using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class OneDriveLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?(?:1drv\.ms/|onedrive\.live\.com/(?:download|redir))",
                RegexOptions.IgnoreCase);

        public OneDriveLoader(string url) : base(url) {}

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            Logging.Debug(Url);

            if (Regex.IsMatch(Url, @"^https?://(?:www\.)?1drv\.ms/", RegexOptions.IgnoreCase)) {
                Logging.Debug("Shortened mode");

                var request = (HttpWebRequest)WebRequest.Create(Url);
                request.Method = "GET";
                request.AllowAutoRedirect = false;
                using (var response = (HttpWebResponse)await request.GetResponseAsync()) {
                    if (response.StatusCode != HttpStatusCode.MovedPermanently) {
                        NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, "Microsoft OneDrive has changed.");
                        return false;
                    }

                    Url = response.Headers["Location"];
                    Logging.Debug(Url);
                }
            }

            if (Regex.IsMatch(Url, @"^https?://(?:www\.)?onedrive\.live\.com/redir", RegexOptions.IgnoreCase)) {
                Url = Url.Replace("/redir", "/download");
                Logging.Debug(Url);
            }

            return true;
        }
    }
}