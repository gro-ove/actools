using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class MediaFireLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?mediafire\.com/file/", RegexOptions.IgnoreCase);

        public MediaFireLoader(string url) : base(url) {}

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            for (var i = 0; i < 5; i++) {
                var str = await client.DownloadStringTaskAsync(Url);
                if (cancellation.IsCancellationRequested) return false;

                var f = Regex.Match(str, @"<div class=""fileName"">([^<]+)");
                FileName = f.Success ? f.Groups[1].Value : null;

                var m = Regex.Match(str, @"(http:\/\/download[^""']+)");
                if (!m.Success) return false;

                Url = m.Success ? m.Groups[1].Value : null;

                if (Url != null) {
                    Logging.Debug("HEAD request is coming…");
                    try {
                        using (client.SetMethod("HEAD"))
                        using (client.SetAutoRedirect(false)) {
                            await client.DownloadStringTaskAsync(Url);
                            Logging.Debug("Done");
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                        return true;
                    }

                    var contentType = client.ResponseHeaders?.Get("Content-Type");
                    Logging.Debug("Content-Type: " + contentType);
                    if (contentType?.IndexOf("text/html") != -1) {
                        Logging.Debug("Redirect to web-page detected! Let’s try again");
                        continue;
                    }
                }

                return true;
            }

            Logging.Warning("Too many redirects!");
            return false;
        }
    }
}