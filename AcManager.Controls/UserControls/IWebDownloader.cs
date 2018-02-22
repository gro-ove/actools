using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public interface IWebDownloader {
        [NotNull, ItemCanBeNull]
        Task<string> Download([NotNull] string destination, [CanBeNull] IProgress<long> progress, CancellationToken cancellation);
    }
}