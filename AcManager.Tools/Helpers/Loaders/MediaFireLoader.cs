using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.Loaders {
    internal class MediaFireLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?mediafire\.com/file/", RegexOptions.IgnoreCase);

        public MediaFireLoader(string url) : base(url) {}

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            var str = await client.DownloadStringTaskAsync(Url);
            if (cancellation.IsCancellationRequested) return false;

            var f = Regex.Match(str, @"<div class=""fileName"">([^<]+)");
            FileName = f.Success ? f.Groups[1].Value : null;

            var m = Regex.Match(str, @"(http:\/\/download[^""']+)");
            if (!m.Success) return false;

            Url = m.Success ? m.Groups[1].Value : null;
            return true;
        }
    }
}