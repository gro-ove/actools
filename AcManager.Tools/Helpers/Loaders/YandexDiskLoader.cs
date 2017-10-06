using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Loaders {
    internal class YandexDiskLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?yadi\.sk/d/", RegexOptions.IgnoreCase);

        public YandexDiskLoader(string url) : base(url) {}

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            var description = await client.DownloadStringTaskAsync(
                    "https://cloud-api.yandex.net:443/v1/disk/public/resources/download?public_key=" + HttpUtility.UrlEncode(Url));
            if (cancellation.IsCancellationRequested) return false;

            Url = (string)JObject.Parse(description)["href"];
            Logging.Write("Yandex Disk download link: " + Url);

            try {
                var query = HttpUtility.ParseQueryString(Url);
                FileName = query["filename"];
                if (FlexibleParser.TryParseLong(query["fsize"], out var size)) {
                    TotalSize = size;
                }
            } catch (Exception) {
                // ignored
            }

            return true;
        }
    }
}