using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public class DirectoryContentInstallator : IAdditionalContentInstallator {
        public string Directory { get; }

        private DirectoryContentInstallator(string directory) {
            Directory = directory;
        }

        public static async Task<IAdditionalContentInstallator> Create(string directory) {
            var result = new DirectoryContentInstallator(directory);
            await result.ScanAsync();
            return result;
        }

        private Task ScanAsync() {
            return Task.Run(() => {

            });
        }

        public Task<IReadOnlyList<AdditionalContentEntry>> GetEntriesAsync(IProgress<string> progress, CancellationToken cancellation) {
            throw new NotImplementedException();
        }

        public Task InstallEntryToAsync(AdditionalContentEntry entry, Func<string, bool> filter, string targetDirectory, IProgress<string> progress, CancellationToken cancellation) {
            throw new NotImplementedException();
        }

        public void Dispose() {
        }

        public string Password => null;

        public bool IsPasswordRequired => false;

        public bool IsPasswordCorrect => true;

        public Task TrySetPasswordAsync(string password) {
            throw new NotSupportedException();
        }
    }
}