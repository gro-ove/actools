using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using HtmlAgilityPack;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AdFlyLoader : RedirectingLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?adf\.ly/", RegexOptions.IgnoreCase);

        public AdFlyLoader(string url) : base(url) {}

        protected override async Task<string> GetRedirectOverrideAsync(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            using (client.SetUserAgent("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")) {
                var redirectTo = Reverse(Regex.Match(await client.DownloadStringTaskAsync(url), @"ysmm\s=\s'(.*?)'").Groups[1].Value);
                if (cancellation.IsCancellationRequested) return redirectTo;

                if (Test(redirectTo)) {
                    redirectTo = Unwrap(await client.DownloadStringTaskAsync(redirectTo)) ?? redirectTo;
                    if (cancellation.IsCancellationRequested) return redirectTo;
                }

                using (var stream = await client.OpenReadTaskAsync(redirectTo)) {
                    if (cancellation.IsCancellationRequested) return redirectTo;
                    if (client.ResponseHeaders?.Get("Content-Type").Contains(@"text/html", StringComparison.OrdinalIgnoreCase) == true) {
                        redirectTo = Unwrap((await stream.ReadAsBytesAsync()).ToUtf8String()) ?? redirectTo;
                    }
                }

                return redirectTo;
            }

            string Unwrap(string html) {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                return doc.DocumentNode.Descendants(@"a")
                                 .FirstOrDefault(x => x.InnerText.Contains(@"click"))?
                                 .Attributes[@"href"]?.Value;
            }
        }

        private static string Reverse(string input) {
            var value = input;
            string f = "", r = "";
            for (var m = 0; m < value.Length; m++) {
                if (m % 2 == 0) {
                    f += value[m];
                } else {
                    r = value[m] + r;
                }
            }
            value = f + r;

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++) {
                if (!char.IsDigit(chars[i])) continue;
                for (var j = i + 1; j < chars.Length; j++) {
                    if (!char.IsDigit(chars[j])) continue;

                    var s = (chars[i] - '0') ^ (chars[j] - '0');
                    if (s < 10) {
                        chars[i] = s.ToInvariantString()[0];
                    }
                    i = j;
                    j = chars.Length;
                }
            }

            value = Encoding.UTF8.GetString(Convert.FromBase64String(new string(chars)));
            value = value.Substring(16, value.Length - 32);
            return value;
        }
    }
}