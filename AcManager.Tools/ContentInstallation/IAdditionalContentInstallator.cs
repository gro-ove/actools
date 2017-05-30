using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public interface IAdditionalContentInstallator : IDisposable {
        string Password { get; }

        bool IsPasswordRequired { get; }

        bool IsNotSupported { get; }

        string NotSupportedMessage { get; }

        bool IsPasswordCorrect { get; }

        Task TrySetPasswordAsync(string password, CancellationToken cancellation);

        [ItemCanBeNull]
        Task<IReadOnlyList<ContentEntryBase>> GetEntriesAsync([CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);

        Task InstallEntryToAsync(CopyCallback callback, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);

        Task InstallEntryToAsync(ContentEntryBase entryBase, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }
}
