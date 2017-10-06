using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AdFlyLoader : RedirectingLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?adf\.ly/", RegexOptions.IgnoreCase);

        public AdFlyLoader(string url) : base(url) {}

        protected override async Task<string> GetRedirect(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            using (client.SetUserAgent("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")) {
                return Reverse(Regex.Match(await client.DownloadStringTaskAsync(url), @"ysmm\s=\s'(.*?)'").Groups[1].Value);
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