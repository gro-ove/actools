using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Loaders {
    public abstract class RedirectingLoader : ILoader {
        private string _url;
        private ILoader _innerLoader;

        public ILoader Parent { get; set; }

        protected RedirectingLoader(string url) {
            _url = url;
        }

        public Task<string> GetRedirectAsync(string url, CookieAwareWebClient client, CancellationToken cancellation) {
            return GetRedirectOverrideAsync(url, client, cancellation);
        }

        protected abstract Task<string> GetRedirectOverrideAsync(string url, CookieAwareWebClient client, CancellationToken cancellation);

        protected long? OverrideTotalSize { get; set; }
        protected string OverrideFileName { get; set; }
        protected string OverrideVersion { get; set; }

        public async Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            var url = await GetRedirectOverrideAsync(_url, client, cancellation);
            if (url == null || cancellation.IsCancellationRequested) return false;

            _url = url;
            Logging.Write($"{GetType().Name} download link: {_url}");

            _innerLoader = await FlexibleLoader.CreateLoaderAsync(_url, this, cancellation);
            if (_innerLoader == null || cancellation.IsCancellationRequested) return false;

            if (_innerLoader.GetType() == GetType()) throw new Exception(ToolsStrings.DirectLoader_RecursionDetected);
            return await _innerLoader.PrepareAsync(client, cancellation);
        }

        public long? TotalSize => OverrideTotalSize ?? _innerLoader?.TotalSize;
        public string FileName => OverrideFileName ?? _innerLoader?.FileName;
        public string Version => OverrideVersion ?? _innerLoader?.Version;
        public bool UsesClientToDownload => _innerLoader?.UsesClientToDownload ?? true;

        public bool CanPause => _innerLoader?.CanPause ?? false;

        public Task<string> DownloadAsync(CookieAwareWebClient client,
                FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused,
                IProgress<long> progress, CancellationToken cancellation) {
            return _innerLoader.DownloadAsync(client, getPreferredDestination, reportDestination, checkIfPaused, progress, cancellation);
        }

        public Task<string> GetDownloadLink(CancellationToken cancellation) {
            return _innerLoader.GetDownloadLink(cancellation);
        }
    }
}