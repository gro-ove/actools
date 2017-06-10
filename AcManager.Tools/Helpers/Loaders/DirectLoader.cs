using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.Loaders {
    internal class DirectLoader : ILoader {
        protected string Url;

        public virtual long TotalSize { get; protected set; } = -1L;

        private string _fileName;

        public virtual string FileName {
            get => _fileName;
            protected set => _fileName = value;
        }

        public virtual string Version { get; protected set; }

        public DirectLoader(string url) {
            _fileName = Path.GetFileName(url);
            Url = url;
        }

        public virtual Task<bool> PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
            return Task.FromResult(true);
        }

        public virtual async Task DownloadAsync(CookieAwareWebClient client, string destination, CancellationToken cancellation) {
            await client.DownloadFileTaskAsync(Url, destination);
        }

        public virtual Task<string> GetDownloadLink(CancellationToken cancellation) {
            return Task.FromResult(Url);
        }
    }
}