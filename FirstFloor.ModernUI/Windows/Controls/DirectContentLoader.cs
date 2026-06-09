using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(Entries))]
    public class DirectContentLoader : IContentLoader {
        public List<DirectContentLoaderEntry> Entries { get; }

        public DirectContentLoader() {
            Entries = new List<DirectContentLoaderEntry>(3);
        }

        public Task<object> LoadContentAsync(Uri uri, CancellationToken cancellationToken) {
            return Task.FromResult(LoadContent(uri));
        }

        public object LoadContent(Uri uri) {
            return Entries.FirstOrDefault(x => string.Equals(x.Source.ToString(), uri.OriginalString, StringComparison.OrdinalIgnoreCase))?.Content;
        }
    }
}