using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class AssettoDbLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?assetto-db.com/", RegexOptions.IgnoreCase);

        public AssettoDbLoader(string url) : base(url) { }

        public override async Task<bool> PrepareAsync(WebClient client, CancellationToken cancellation) {
            var downloadPage = await client.DownloadStringTaskAsync(Url);
            if (cancellation.IsCancellationRequested) return false;

            var match = Regex.Match(downloadPage, @"href=""(/[^""]+/download)""");
            if (!match.Success) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_AssettoDbChanged);
                return false;
            }

            Url = "http://assetto-db.com" + HttpUtility.HtmlDecode(match.Groups[1].Value);
            Logging.Write("Assetto-DB.com download link: " + Url);
            return true;
        }
    }
}