using System.Collections.Specialized;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Internal;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class RaceDepartmentLoader : DirectLoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?racedepartment.com/downloads/", RegexOptions.IgnoreCase);

        public RaceDepartmentLoader(string url) : base(url) { }

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            var downloadPage = await client.DownloadStringTaskAsync(Url);
            if (cancellation.IsCancellationRequested) return false;

            var match = Regex.Match(downloadPage, @"href=""(downloads/[^""]+\?version=[^""]+)");
            if (!match.Success) {
                NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_RdChanged);
                return false;
            }

            var url = "http://www.racedepartment.com/" + HttpUtility.HtmlDecode(match.Groups[1].Value);

            // Why, RD, why?!
            try {
                using (client.SetMethod("HEAD")) {
                    await client.DownloadStringTaskAsync(url);
                }
            } catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden) {
                var login = SettingsHolder.Content.RdLogin;
                var password = SettingsHolder.Content.RdPassword;
                await client.UploadValuesTaskAsync("http://www.racedepartment.com/login/login",
                        string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password)
                                ? InternalUtils.GetRdLoginParams()
                                : new NameValueCollection {
                                    ["login"] = login,
                                    ["password"] = password,
                                });
            }

            Url = url;
            Logging.Write("RaceDepartment download link: " + Url);
            return true;
        }
    }
}