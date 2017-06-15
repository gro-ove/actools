using System;
using System.Collections.Specialized;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Internal;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    public class RaceDepartmentLoader : DirectLoader {
        public static bool OptionFailImmediately;

        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?racedepartment\.com/downloads/", RegexOptions.IgnoreCase);

        static RaceDepartmentLoader() {
            ServicePointManager.Expect100Continue = false;
        }

        public RaceDepartmentLoader(string url) : base(url) { }

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            if (OptionFailImmediately) {
                throw new NotSupportedException();
            }

            async Task Login() {
                var login = SettingsHolder.Content.RdLogin;
                var password = SettingsHolder.Content.RdPassword;
                var loginParams = string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password)
                        ? InternalUtils.GetRdLoginParams()
                        : new NameValueCollection {
                            ["login"] = login,
                            ["password"] = password,
                        };

                Logging.Debug($"Forbidden! Trying to login with provided params ({loginParams["login"]})");
                var result = (await client.UploadValuesTaskAsync("http://www.racedepartment.com/login/login", loginParams)).ToUtf8String();

                var error = Regex.Match(result, @"<div class=""errorPanel""><span class=""errors"">([\s\S]+?)(?:</span>\s*)?</div>");
                if (error.Success) {
                    throw new Exception(error.Groups[1].Value);
                }
            }

            using (client.SetProxy(SettingsHolder.Content.RdProxy)) {
                var downloadPage = await client.DownloadStringTaskAsync(Url);
                if (cancellation.IsCancellationRequested) return false;

                var match = Regex.Match(downloadPage, @"href=""(downloads/[^""]+\?version=[^""]+)");
                if (!match.Success) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotDownloadFile, ToolsStrings.DirectLoader_RdChanged);
                    return false;
                }

                if (Regex.IsMatch(downloadPage, @"""inner"">\s*Login to download this mod")) {
                    await Login();
                }

                var url = "http://www.racedepartment.com/" + HttpUtility.HtmlDecode(match.Groups[1].Value);

                // Why, RD, why?!
                try {
                    using (client.SetMethod("HEAD")) {
                        await client.DownloadStringTaskAsync(url);
                    }
                } catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.Forbidden) {
                    await Login();
                }

                Url = url;
                Logging.Write("RaceDepartment download link: " + Url);
            }

            return true;
        }

        public override async Task DownloadAsync(CookieAwareWebClient client, string destination, CancellationToken cancellation) {
            if (OptionFailImmediately) {
                throw new NotSupportedException();
            }

            using (client.SetProxy(SettingsHolder.Content.RdProxy)) {
                await client.DownloadFileTaskAsync(Url, destination);
            }
        }
    }
}