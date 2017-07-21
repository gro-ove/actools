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

        bool IsReady { get; }

        bool SupportsSigning { get; }

        bool SupportsDirectories { get; }

        [CanBeNull]
        string DestinationDirectoryId { get; set; }

        Task Reset();

        Task Prepare(CancellationToken cancellation);

        Task SignIn(CancellationToken cancellation);

        [ItemNotNull]
        Task<DirectoryEntry[]> GetDirectories(CancellationToken cancellation);

        [ItemNotNull]
        Task<UploadResult> Upload([NotNull] string name, [NotNull] string originalName, [NotNull] string mimeType, [CanBeNull] string description,
                [NotNull] Stream data, UploadAs uploadAs, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }
}
