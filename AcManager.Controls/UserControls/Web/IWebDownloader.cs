using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Web {
    public interface IWebDownloader {
        [NotNull, ItemCanBeNull]
        Task<string> DownloadAsync([NotNull] string destination, [CanBeNull] IProgress<long> progress, CancellationToken cancellation);
    }
}