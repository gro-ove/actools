using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class GoogleDriveLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://drive\.google\.com/", RegexOptions.IgnoreCase);

        private static string PrepareUrl(string url) {
            var openMatch = Regex.Match(url, @"://drive\.google\.com/open\b.*\bid=([\w-]+)", RegexOptions.IgnoreCase);
            if (openMatch.Success) {
                return "https://drive.google.com/uc?export=download&id=" + openMatch.Groups[1].Value;
            }

            var dMatch = Regex.Match(url, @"://drive\.google\.com/file/d/([\w-]+)", RegexOptions.IgnoreCase);
            return dMatch.Success ? "https://drive.google.com/uc?export=download&id=" + dMatch.Groups[1].Value : url;
        }

        public GoogleDriveLoader(string url) : base(PrepareUrl(url)) {}

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            Logging.Debug(Url);
            if (!Url.Contains("://drive.google.com/uc?", StringComparison.OrdinalIgnoreCase)) return true;

            // First of all, let’s see if there is an HTML-file under that link
            Logging.Debug("HEAD request is coming…");
            try {
                using (client.SetMethod("HEAD"))
                using (client.SetAutoRedirect(false)) {
                    await client.DownloadStringTaskAsync(Url);
                    Logging.Debug("Done");
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            // If file is freely available to download, server should redirect user to downloading
            var location = client.ResponseHeaders?.Get("Location");
            if (location != null) {
                Url = location;
                Logging.Debug("Download URL is ready: " + location);
                return true;
            }

            Logging.Debug("Loading page…");
            var downloadPage = await client.DownloadStringTaskAsync(Url);
            if (cancellation.IsCancellationRequested) return false;

            if (client.ResponseHeaders?.Get("Content-Type").Contains("text/html", StringComparison.OrdinalIgnoreCase) == false) return true;
            var match = Regex.Match(downloadPage, @"href=""(/uc\?export=download[^""]+)", RegexOptions.IgnoreCase);
            if (!match.Success) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_GoogleDriveChanged);
                return false;
            }

            Url = "https://drive.google.com" + HttpUtility.HtmlDecode(match.Groups[1].Value);
            Logging.Write("Google Drive download link: " + Url);

            var fileNameMatch = Regex.Match(downloadPage, @"/<span class=""uc-name-size""><a[^>]*>([^<]+)");
            FileName = fileNameMatch.Success ? fileNameMatch.Groups[1].Value : null;

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