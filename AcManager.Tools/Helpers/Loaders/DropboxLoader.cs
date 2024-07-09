using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class DropboxLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?dropbox\.com/(?:s|scl)/", RegexOptions.IgnoreCase);

        public DropboxLoader(string url) : base(url) {}

        public override Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            // https://www.dropbox.com/scl/fi/et8u9rzw9wnxu0bq5eh1y/Ferrari-812-Superfast-Gintani?rlkey=f9phxyaan5h9x794dzn3fx61j&dl=1
            Url = $@"{Regex.Match(Url, @"^[^?]+")}?{Regex.Match(Url, @"\?.+").Value.TrimStart('?')
                    .Split('&').Where(x => !x.StartsWith(@"dl=")).Append(@"dl=1").JoinToString('&')}";
            return Task.FromResult(true);
        }
    }
}