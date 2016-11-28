using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api.Kunos;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public interface IOnlineSource {
        string Key { get; }

        bool IsBackgroundLoadable { get; }

        /// <summary>
        /// Throws exceptions.
        /// </summary>
        /// <returns>Task.</returns>
        Task LoadAsync([NotNull] Action<ServerInformation> callback, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }
}