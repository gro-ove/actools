using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using CG.Web.MegaApiClient;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal class MegaLoader : ILoader {
        // https://mega.nz/file/55MFAa4Z#uIILkF1kpXekTS0L8sxBGS5zzCEc00LLidIOVM2-Vt8
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?mega(?:\.co)?\.nz/(?:#|file/)", RegexOptions.IgnoreCase);

        private readonly Uri _uri;
        private MegaApiClient _client;

        public MegaLoader(string url) {
            var match = Regex.Match(url, @"\.nz/file/([^#]{4,12})#(.+)");
            if (match.Success) {
                url = $@"https://mega.nz/#!{match.Groups[1]}!{match.Groups[2]}";
            }
            _uri = new Uri(url);
        }

        public ILoader Parent { get; set; }
        public long? TotalSize { get; private set; }
        public string FileName { get; private set; }
        public string Version => null;

        private const string KeySession = "session";
        private const string KeyToken = "token";

        public async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            _client = new MegaApiClient(new Options(InternalUtils.GetMegaAppKey().Item1));

            var storage = SettingsHolder.Content.MegaAuthenticationStorage;
            var token = new MegaApiClient.LogonSessionToken(
                    storage.GetEncrypted<string>(KeySession),
                    storage.GetEncrypted<byte[]>(KeyToken));
            if (!string.IsNullOrWhiteSpace(token.SessionId) && token.MasterKey != null) {
                await _client.LoginAsync(token);
            } else {
                await _client.LoginAnonymousAsync();
            }

            try {
                var information = await _client.GetNodeFromLinkAsync(_uri);
                TotalSize = information.Size;
                FileName = information.Name;
                return true;
            } catch (ApiException e) {
                WindowsHelper.ViewInBrowser(_uri);
                throw new InformativeException("Unsupported link", "Please download it manually.", e);
            }
        }

        public bool UsesClientToDownload => false;
        public bool CanPause => false;

        public async Task<string> DownloadAsync(CookieAwareWebClient client,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
            try {
                // TODO: Resume download?
                var d = getPreferredDestination(_uri.OriginalString, FlexibleLoaderMetaInformation.FromLoader(this));
                if (File.Exists(d.Filename)) {
                    if (d.CanResumeDownload && new FileInfo(d.Filename).Length == TotalSize) {
                        return d.Filename;
                    }

                    File.Delete(d.Filename);
                }

                await _client.DownloadFileAsync(_uri, d.Filename, new Progress<double>(x => progress?.Report((long)((TotalSize ?? 0d) * x / 100))), cancellation);
                return d.Filename;
            } catch (Exception e) when (IsBandwidthLimitExceeded(e)) {
                throw new InformativeException("Bandwidth limit exceeded", "That’s Mega.nz for you.", e);
            } catch (ApiException e) {
                WindowsHelper.ViewInBrowser(_uri);
                throw new InformativeException("Unsupported link", "Please download it manually.", e);
            }

            bool IsBandwidthLimitExceeded(Exception e) {
                if (e is AggregateException ae) {
                    return IsBandwidthLimitExceeded(ae.InnerException) || ae.InnerExceptions.Any(IsBandwidthLimitExceeded);
                }

                return e is HttpRequestException && e.Message.Contains(@"509");
            }
        }

        public Task<string> GetDownloadLink(CancellationToken cancellation) {
            return Task.FromResult(_uri.OriginalString);
        }
    }
}