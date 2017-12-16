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

        Task<bool> PrepareAsync([NotNull] CookieAwareWebClient client, CancellationToken cancellation);

        Task<string> DownloadAsync([NotNull] CookieAwareWebClient client, [NotNull] FlexibleLoaderDestinationCallback destinationCallback,
                [CanBeNull] Func<bool> pauseCallback, [CanBeNull] IProgress<long> progress, CancellationToken cancellation);

        Task<string> GetDownloadLink(CancellationToken cancellation);
    }
}