using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;

namespace AcManager.Tools.Helpers.Loaders {
    internal class MegaLoader : ILoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?mega(?:\.co)?\.nz/#", RegexOptions.IgnoreCase);

        private readonly Uri _uri;
        private MegaApiClient _client;

        public MegaLoader(string url) {
            _uri = new Uri(url);
        }

        public ILoader Parent { get; set; }
        public long? TotalSize { get; private set; }
        public string FileName { get; private set; }
        public string Version => null;

        public async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            _client = new MegaApiClient();
            await _client.LoginAnonymousAsync();

            var information = await _client.GetNodeFromLinkAsync(_uri);
            TotalSize = information.Size;
            FileName = information.Name;
            return true;
        }

        public bool UsesClientToDownload => false;
        public bool CanPause => false;

        public async Task<string> DownloadAsync(CookieAwareWebClient client,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
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
        }

        public Task<string> GetDownloadLink(CancellationToken cancellation) {
            return Task.FromResult(_uri.OriginalString);
        }
    }
}