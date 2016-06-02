using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class RaceDepartmentLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?racedepartment.com/downloads/", RegexOptions.IgnoreCase);

        public RaceDepartmentLoader(string url) : base(url) { }

        public override async Task<bool> PrepareAsync(WebClient client, CancellationToken cancellation) {
            var downloadPage = await client.DownloadStringTaskAsync(Url);
            if (cancellation.IsCancellationRequested) return false;

            var match = Regex.Match(downloadPage, @"href=""(downloads/[^""]+\?version=[^""]+)");
            if (!match.Success) {
                NonfatalError.Notify("Can’t download file", "Assetto-DB.com is changed.");
                return false;
            }

            Url = "http://www.racedepartment.com/" + HttpUtility.HtmlDecode(match.Groups[1].Value);
            Logging.Write("RaceDepartment download link: " + Url);
            return true;
        }
    }
}