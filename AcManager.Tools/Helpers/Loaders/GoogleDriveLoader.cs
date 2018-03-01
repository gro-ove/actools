using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using HtmlAgilityPack;

namespace AcManager.Tools.Helpers.Loaders {
    public class GoogleDriveLoader : DirectLoader {
        public static bool OptionManualRedirect = false;
        public static bool OptionDebugMode = false;

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

        protected override bool? ResumeSupported => true;
        protected override bool HeadRequestSupported => false;

        public override string GetFootprint(FlexibleLoaderMetaInformation information, WebHeaderCollection headers) {
            return $"filename={information.FileName}, size={information.TotalSize}, checksum={headers?["X-Goog-Hash"]}".ToCutBase64();
        }

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            Logging.Debug(Url);
            if (!Url.Contains("://drive.google.com/uc?", StringComparison.OrdinalIgnoreCase)) return true;

            // First of all, let’s see if there is an HTML-file under that link
            Logging.Debug("GET request is coming…");
            string webPageContent;

            using (client.SetAutoRedirect(false))
            using (var stream = await client.OpenReadTaskAsync(Url)) {
                if (cancellation.IsCancellationRequested) return false;

                // If file is freely available to download, server should redirect user to downloading
                var location = client.ResponseHeaders?.Get("Location");
                if (location != null) {
                    Url = location;
                    FileName = new Uri(Url, UriKind.RelativeOrAbsolute).GetQueryParam("id");
                    Logging.Debug("Download URL is ready: " + location);
                    client.LogResponseHeaders();
                    return true;
                }

                Logging.Debug("Content-Type: " + client.ResponseHeaders?.Get("Content-Type"));
                if (client.ResponseHeaders?.Get("Content-Type").Contains("text/html", StringComparison.OrdinalIgnoreCase) == false) return true;

                // Looks like it’s a webpage, now we need to download and parse it
                webPageContent = (await stream.ReadAsBytesAsync()).ToUtf8String();
                if (cancellation.IsCancellationRequested) return false;

                Logging.Debug("…done");
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(webPageContent);

            var link = doc.DocumentNode.SelectSingleNode(@"//a[contains(@href, 'export=download')]").Attributes[@"href"].Value;
            if (link == null) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_GoogleDriveChanged);
                return false;
            }

            Url = @"https://drive.google.com" + HttpUtility.HtmlDecode(link);
            FileName = HttpUtility.HtmlDecode(doc.DocumentNode.SelectSingleNode(@"//span[@class='uc-name-size']/a")?.InnerText?.Trim());
            Logging.Write($"Google Drive download link: {Url}");

            try {
                var totalSize = HttpUtility.HtmlDecode(
                        doc.DocumentNode.SelectSingleNode(@"//span[@class='uc-name-size']/text()")?.InnerText?.Trim(' ', '(', ')'));
                Logging.Write($"Total size: {totalSize}");
                if (totalSize != null && LocalizationHelper.TryParseReadableSize(totalSize, null, out var size)) {
                    Logging.Write($"Parsed size: {size} bytes");
                    TotalSize = size;
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            if (OptionManualRedirect) {
                using (client.SetDebugMode(OptionDebugMode))
                using (client.SetAutoRedirect(false)) {
                    var redirect = await client.DownloadStringTaskAsync(Url);
                    Logging.Debug(redirect);

                    if (!redirect.Contains("<TITLE>Moved Temporarily</TITLE>")) {
                        NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_GoogleDriveChanged);
                        return false;
                    }

                    var redirectMatch = Regex.Match(redirect, @"href=""([^""]+)", RegexOptions.IgnoreCase);
                    if (!redirectMatch.Success) {
                        NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_GoogleDriveChanged);
                        return false;
                    }

                    Url = HttpUtility.HtmlDecode(redirectMatch.Groups[1].Value);
                    Logging.Debug(Url);
                }
            }

            return true;
        }

        protected override async Task<string> DownloadAsyncInner(CookieAwareWebClient client,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
            using (client.SetDebugMode(OptionDebugMode))
            using (client.SetAutoRedirect(!OptionManualRedirect)) {
                return await base.DownloadAsyncInner(client, getPreferredDestination, reportDestination, checkIfPaused, progress, cancellation);
            }
        }
    }
}