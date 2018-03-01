using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AcStuffSharedLoader : RedirectingLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?acstuff\.ru/s/", RegexOptions.IgnoreCase);

        public AcStuffSharedLoader(string url) : base(url) { }

        protected override async Task<string> GetRedirectOverrideAsync(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            using (client.SetAccept("application/json")) {
                var response = await client.DownloadStringTaskAsync(url);
                if (cancellation.IsCancellationRequested) return null;
                return JObject.Parse(response).GetStringValueOnly("request");
            }
        }
    }
}