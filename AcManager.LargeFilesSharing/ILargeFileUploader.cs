using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing {
    public interface ILargeFileUploader : IWithId, INotifyPropertyChanged {
        [NotNull]
        string DisplayName { get; }

        [NotNull]
        string Description { get; }

        [CanBeNull]
        Uri Icon { get; }

        bool IsReady { get; }

        bool SupportsSigning { get; }

        bool SupportsDirectories { get; }

        [CanBeNull]
        string DestinationDirectoryId { get; set; }

        Task PrepareAsync(CancellationToken cancellation);

        Task SignInAsync(CancellationToken cancellation);

        Task ResetAsync(CancellationToken cancellation);

        [ItemNotNull]
        Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation);

        [ItemNotNull]
        Task<UploadResult> UploadAsync([NotNull] string name, [NotNull] string originalName, [NotNull] string mimeType, [CanBeNull] string description,
                [NotNull] Stream data, UploadAs uploadAs, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }
}
