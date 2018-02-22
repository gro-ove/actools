using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public interface IWebDownloadListener {
        void OnDownload([NotNull] string url, [CanBeNull] string suggestedName, long totalSize, [NotNull] IWebDownloader downloader);
    }
}