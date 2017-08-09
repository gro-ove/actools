using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Entries;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Installators {
    public class InstallationDetails {
        [NotNull]
        public readonly ICopyCallback CopyCallback;

        [NotNull]
        public readonly string[] ToRemoval;

        [CanBeNull]
        public readonly Func<CancellationToken, Task> BeforeTask, AfterTask;

        public InstallationDetails([NotNull] ICopyCallback copyCallback, [CanBeNull] string[] toRemoval,
                [CanBeNull] Func<CancellationToken, Task> beforeTask, [CanBeNull] Func<CancellationToken, Task> afterTask) {
            CopyCallback = copyCallback;
            ToRemoval = toRemoval ?? new string[0];
            BeforeTask = beforeTask;
            AfterTask = afterTask;
        }

        internal ContentEntryBase OriginalEntry { get; set; }
    }
}