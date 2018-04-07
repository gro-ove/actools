using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Loaders {
    internal class YouTubeDescriptionLoader : RedirectingLoader {
        private static readonly Regex TestRegex = new Regex(@"^https?://(?:www\.)?youtube\.com/watch\?v=([^&]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool Test(string url) {
            return TestRegex.IsMatch(url);
        }

        public YouTubeDescriptionLoader(string url) : base(url) { }

        protected override async Task<string> GetRedirectOverrideAsync(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            var id = TestRegex.Match(url).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(id)) {
                return null;
            }

            var data = await client.DownloadStringTaskAsync(
                    $@"https://www.googleapis.com/youtube/v3/videos?part=snippet&id={id}&key={InternalUtils.GetYouTubeApiKey().Item1}");
            if (cancellation.IsCancellationRequested) return null;

            try {
                var links = JObject.Parse(data)[@"items"][0][@"snippet"][@"description"].ToString().GetUrls().ToList();
                var supported = links.FirstOrDefault(FlexibleLoader.IsSupportedFileStorage);
                if (supported != null) {
                    return supported;
                }

                foreach (var link in links) {
                    if (cancellation.IsCancellationRequested) return null;
                    if (await FlexibleLoader.IsSupportedAsync(link, cancellation)) {
                        return link;
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e);
                throw;
            }

            return null;
        }
    }
}