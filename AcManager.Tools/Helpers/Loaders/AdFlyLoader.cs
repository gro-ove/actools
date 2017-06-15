using System;
using System.Collections.Generic;
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
                return Encoding.UTF8.GetString(Convert.FromBase64String(Reverse(
                        Regex.Match(await client.DownloadStringTaskAsync(url), @"ysmm\s=\s'(.*?)'").Groups[1].Value))).Remove(0, 2);
            }

        }

        private static string Reverse(string input) {
            var begin = new List<char>(input.Length / 2);
            var end = new List<char>(input.Length / 2);

            for (var i = 0; i <= input.Length - 1; i += 2) {
                begin.Add(input[i]);
                end.Add(input[i + 1]);
            }

            end.Reverse();
            return string.Concat(begin.JoinToString(), end.JoinToString());
        }
    }
}