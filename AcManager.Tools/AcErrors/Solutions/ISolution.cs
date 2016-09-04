using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors.Solutions {
    public interface ISolution {
        [NotNull]
        string Name { get; }

        [CanBeNull]
        string Description { get; }
        
        bool IsUiSolution { get; }

        Task Run([NotNull] IAcError error, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }
}