using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.Loaders {
    internal class DirectLoader : ILoader {
        protected string Url;

        public virtual long TotalSize { get; protected set; } = -1L;

        public DirectLoader(string url) {
            Url = url;
        }

        public virtual Task<bool> PrepareAsync(WebClient client, CancellationToken cancellation) {
            return Task.FromResult(true);
        }

        public virtual async Task DownloadAsync(WebClient client, string destination, CancellationToken cancellation) {
            await client.DownloadFileTaskAsync(Url, destination);
        }
    }
}