using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public interface ILoader {
        long? TotalSize { get; }

        [CanBeNull]
        string FileName { get; }

        [CanBeNull]
        string Version { get; }

        bool UsesClientToDownload { get; }
        bool CanPause { get; }

        Task<bool> PrepareAsync([NotNull] CookieAwareWebClient client, CancellationToken cancellation);

        Task<string> DownloadAsync([NotNull] CookieAwareWebClient client,
                [NotNull] FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                [CanBeNull] FlexibleLoaderReportDestinationCallback reportDestination, [CanBeNull] Func<bool> checkIfPaused,
                [CanBeNull] IProgress<long> progress, CancellationToken cancellation);

        Task<string> GetDownloadLink(CancellationToken cancellation);
    }
}