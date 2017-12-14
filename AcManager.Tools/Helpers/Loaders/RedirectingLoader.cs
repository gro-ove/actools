using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    internal abstract class RedirectingLoader : ILoader {
        private string _url;
        private ILoader _innerLoader;

        protected RedirectingLoader(string url) {
            _url = url;
        }

        protected abstract Task<string> GetRedirect(string url, CookieAwareWebClient client, CancellationToken cancellation);

        protected long? OverrideTotalSize { get; set; }
        protected string OverrideFileName { get; set; }
        protected string OverrideVersion { get; set; }

        public async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            var url = await GetRedirect(_url, client, cancellation);
            if (url == null || cancellation.IsCancellationRequested) return false;

            _url = url;
            Logging.Write($"{GetType().Name} download link: {_url}");

            _innerLoader = FlexibleLoader.CreateLoader(_url);
            if (_innerLoader.GetType() == GetType()) throw new Exception(ToolsStrings.DirectLoader_RecursionDetected);
            return await _innerLoader.PrepareAsync(client, cancellation);
        }

        public long? TotalSize => OverrideTotalSize ?? _innerLoader?.TotalSize;
        public string FileName => OverrideFileName ?? _innerLoader?.FileName;
        public string Version => OverrideVersion ?? _innerLoader?.Version;
        public bool UsesClientToDownload => _innerLoader?.UsesClientToDownload ?? true;

        public Task<string> DownloadAsync(CookieAwareWebClient client, FlexibleLoaderDestinationCallback destinationCallback, IProgress<long> progress,
                CancellationToken cancellation) {
            return _innerLoader.DownloadAsync(client, destinationCallback, progress, cancellation);
        }

        public Task<string> GetDownloadLink(CancellationToken cancellation) {
            return _innerLoader.GetDownloadLink(cancellation);
        }
    }
}