using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Loaders {
    public interface ILoader {
        long TotalSize { get; }

        [CanBeNull]
        string FileName { get; }

        [CanBeNull]
        string Version { get; }

        bool UsesClientToDownload { get; }

        Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation);

        Task DownloadAsync(CookieAwareWebClient client, string destination, IProgress<double> progress, CancellationToken cancellation);

        Task<string> GetDownloadLink(CancellationToken cancellation);
    }
}