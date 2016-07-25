using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public interface IAdditionalContentInstallator : IDisposable {
        string Password { get; }

        bool IsPasswordRequired { get; }

        bool IsPasswordCorrect { get; }

        Task TrySetPasswordAsync(string password);

        Task<IReadOnlyList<ContentEntry>> GetEntriesAsync([CanBeNull]IProgress<string> progress, CancellationToken cancellation);

        Task InstallEntryToAsync(ContentEntry entry, Func<string, bool> filter, string destination, [CanBeNull]IProgress<string> progress,
                CancellationToken cancellation);
    }
}
