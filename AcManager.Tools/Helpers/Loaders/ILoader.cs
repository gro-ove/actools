using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.Loaders {
    internal interface ILoader {
        long TotalSize { get; }

        Task<bool> PrepareAsync(WebClient client, CancellationToken cancellation);

        Task DownloadAsync(WebClient client, string destination, CancellationToken cancellation);
    }
}