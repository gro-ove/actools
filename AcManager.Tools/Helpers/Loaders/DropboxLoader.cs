using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.Loaders {
    internal class DropboxLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?dropbox\.com/s/", RegexOptions.IgnoreCase);

        public DropboxLoader(string url) : base(url) {}

        public override Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            Url = Regex.Replace(Url, @"\?.+", "") + "?dl=1";
            return Task.FromResult(true);
        }
    }
}