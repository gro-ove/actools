using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Workshop.Uploaders {
    public interface IWorkshopUploader {
        void MarkNewGroup();

        /// <summary>
        /// Uploads a file online for it to be downloaded later, returns a direct link.
        /// </summary>
        /// <param name="data">Data to upload</param>
        /// <param name="downloadName">File name for downloads</param>
        /// <param name="origin">Any tag for the file marking its origin, role or something like that</param>
        /// <param name="progress">Progress callback</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Direct link to download or view content in browser</returns>
        Task<string> UploadAsync([NotNull] byte[] data, [NotNull] string downloadName, [CanBeNull] string origin,
                [CanBeNull] IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default);
    }
}