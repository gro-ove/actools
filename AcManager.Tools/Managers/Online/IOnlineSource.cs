using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    /// <summary>
    /// Online servers’ source.
    /// </summary>
    public interface IOnlineSource : IWithId {
        /// <summary>
        /// Name which will be shown to user.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Fired when list is obsviously obsoleted and requires to be reloaded as soon as possible.
        /// </summary>
        event EventHandler Obsolete;
    }

    /// <summary>
    /// For sources like LAN, which usually load one server at the time.
    /// </summary>
    public interface IOnlineBackgroundSource : IOnlineSource {
        /// <summary>
        /// Throws exceptions.
        /// </summary>
        /// <returns>Task.</returns>
        Task LoadAsync([NotNull] Action<ServerInformation> callback, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }

    /// <summary>
    /// For usual sources, which usually load a list of servers.
    /// </summary>
    public interface IOnlineListSource : IOnlineSource {
        /// <summary>
        /// Throws exceptions.
        /// </summary>
        /// <returns>Task.</returns>
        Task LoadAsync([NotNull] Action<IEnumerable<ServerInformation>> callback, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }
}