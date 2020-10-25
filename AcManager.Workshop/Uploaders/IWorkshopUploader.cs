using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Workshop.Uploaders {
    public class WorkshopUploadResult {
        public long Size { get; set; }
        public string Tag { get; set; }
    }

    public interface IWorkshopUploader {
        /// <summary>
        /// Uploads a file online for it to be downloaded later, returns a file tag to send to Workshop server to get a final link.
        /// </summary>
        /// <param name="data">Data to upload</param>
        /// <param name="group">File group (aka name of its folder), to avoid collisions</param>
        /// <param name="name">File name for downloads</param>
        /// <param name="progress">Progress callback</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Direct link to download or view content in browser</returns>
        Task<WorkshopUploadResult> UploadAsync([NotNull] byte[] data, [NotNull] string group, [NotNull] string name,
                [CanBeNull] IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default);
    }
}