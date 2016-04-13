using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public interface IAdditionalContentInstallator : IDisposable {
        string Password { get; }

        bool IsPasswordRequired { get; }

        bool IsPasswordCorrect { get; }

        Task TrySetPasswordAsync(string password);

        Task<IReadOnlyList<AdditionalContentEntry>> GetEntriesAsync([CanBeNull]IProgress<string> progress, CancellationToken cancellation);

        Task InstallEntryToAsync(AdditionalContentEntry entry, Func<string, bool> filter, string destination, [CanBeNull]IProgress<string> progress,
                CancellationToken cancellation);
    }
}
