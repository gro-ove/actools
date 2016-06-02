using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class GoogleDriveLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://drive\.google\.com/", RegexOptions.IgnoreCase);

        private static string PrepareUrl(string url) {
            var googleDriveMatch = Regex.Match(url, @"://drive\.google\.com/file/d/(\w+)", RegexOptions.IgnoreCase);
            return googleDriveMatch.Success ? "https://drive.google.com/uc?export=download&id=" + googleDriveMatch.Groups[1].Value : url;
        }

        public GoogleDriveLoader(string url) : base(PrepareUrl(url)) {}

        public override async Task<bool> PrepareAsync(WebClient client, CancellationToken cancellation) {
            if (!Url.Contains("://drive.google.com/uc?", StringComparison.OrdinalIgnoreCase)) return true;

            // TODO: drop if page is bigger than, let’s say, 1MB
            var downloadPage = await client.DownloadStringTaskAsync(Url);
            if (cancellation.IsCancellationRequested) return false;

            if (client.ResponseHeaders?.Get("Content-Type").Contains("text/html", StringComparison.OrdinalIgnoreCase) == false) return true;
            var match = Regex.Match(downloadPage, @"href=""(/uc\?export=download[^""]+)", RegexOptions.IgnoreCase);
            if (!match.Success) {
                NonfatalError.Notify("Can’t download file", "Google Drive is changed.");
                return false;
            }

            Url = "https://drive.google.com" + HttpUtility.HtmlDecode(match.Groups[1].Value);
            Logging.Write("Google Drive download link: " + Url);

            try {
                var totalSizeMatch = Regex.Match(downloadPage, @"</a> \((\d+(?:\.\d+)?)([KMGT])\)</span> ");
                if (totalSizeMatch.Success) {
                    var value = double.Parse(totalSizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    var unit = totalSizeMatch.Groups[2].Value;

                    switch (unit.ToLowerInvariant()) {
                        case "k":
                            value *= 1024;
                            break;

                        case "m":
                            value *= 1024 * 1024;
                            break;

                        case "g":
                            value *= 1024 * 1024 * 1024;
                            break;

                        case "t":
                            value *= 1024d * 1024 * 1024 * 1024;
                            break;
                    }

                    TotalSize = (long)value;
                }
            } catch (Exception) {
                // ignored
            }

            return true;
        }
    }
}