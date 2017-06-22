using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;
using CG.Web.MegaApiClient;

namespace AcManager.Tools.Helpers.Loaders {
    internal class MegaLoader : ILoader {
        public static bool Test(string url) => Regex.IsMatch(url, @"^https?://(?:www.)?mega(?:\.co)?\.nz/#", RegexOptions.IgnoreCase);

        private readonly Uri _uri;
        private MegaApiClient _client;

        public MegaLoader(string url) {
            _uri = new Uri(url);
        }

        public long TotalSize { get; private set; }

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

        public Task DownloadAsync(CookieAwareWebClient client, string destination, IProgress<double> progress, CancellationToken cancellation) {
            if (File.Exists(destination)) {
                File.Delete(destination);
            }

            return _client.DownloadFileAsync(_uri, destination, new Progress<double>(x => progress?.Report(x / 100d)), cancellation);
        }

        public Task<string> GetDownloadLink(CancellationToken cancellation) {
            return Task.FromResult(_uri.OriginalString);
        }
    }
}