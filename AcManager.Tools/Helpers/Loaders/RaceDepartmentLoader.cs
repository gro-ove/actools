using System;
using System.Collections.Specialized;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Helpers.Loaders {
    public class RaceDepartmentLoader : DirectLoader {
        public static bool OptionAllowed;
        public static bool OptionFailImmediately;

        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www\.)?racedepartment\.com/downloads/", RegexOptions.IgnoreCase);

        static RaceDepartmentLoader() {
            ServicePointManager.Expect100Continue = false;
        }

        public RaceDepartmentLoader(string url) : base(url) { }

        public override async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            if (!OptionAllowed) {
                throw new InformativeException("Prohibited",
                        $@"CM can’t download files from RaceDepartment anymore. Please, [url={BbCodeBlock.EncodeAttribute(Url)}]download file manually[/url] and then drag’n’drop it to CM.");
            }

            if (OptionFailImmediately) {
                throw new NotSupportedException();
            }

            async Task Login() {
                var login = SettingsHolder.Content.RdLogin;
                var password = SettingsHolder.Content.RdPassword;

                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password)) {
                    throw new InformativeException("RaceDepartment credentials are missing",
                            $@"Go to Settings/Content and put your login and password in, or download file directly from RaceDepartment website.");
                }

                var loginParams = new NameValueCollection {
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

        protected override async Task<string> DownloadAsyncInner(CookieAwareWebClient client,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
            if (OptionFailImmediately) {
                throw new NotSupportedException();
            }

            using (client.SetProxy(SettingsHolder.Content.RdProxy)) {
                return await base.DownloadAsyncInner(client, getPreferredDestination, reportDestination, checkIfPaused, progress, cancellation);
            }
        }
    }
}