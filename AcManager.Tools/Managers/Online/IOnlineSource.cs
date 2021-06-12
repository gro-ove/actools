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
        /// Name which will be shown to user. Immutable.
        /// </summary>
        [NotNull]
        string DisplayName { get; }

        /// <summary>
        /// Fired when list is obsviously obsoleted and requires to be reloaded as soon as possible.
        /// </summary>
        event EventHandler Obsolete;
    }

    public delegate Task ListAddAsyncCallback<in T>([NotNull] IEnumerable<T> value);

    public delegate Task ItemAddAsyncCallback<in T>([NotNull] T value);

    /// <summary>
    /// For sources like LAN, which usually load one server at the time.
    /// </summary>
    public interface IOnlineBackgroundSource : IOnlineSource {
        /// <summary>
        /// Throws exceptions.
        /// </summary>
        /// <returns>True if data is loaded and source can be marked as Ready.</returns>
        Task<bool> LoadAsync([NotNull] ItemAddAsyncCallback<ServerInformation> callback, [CanBeNull] IProgress<AsyncProgressEntry> progress,
                CancellationToken cancellation);
    }

    /// <summary>
    /// For usual sources, which usually load a list of servers.
    /// </summary>
    public interface IOnlineListSource : IOnlineSource {
        /// <summary>
        /// Throws exceptions.
        /// </summary>
        /// <returns>True if data is loaded and source can be marked as Ready.</returns>
        Task<bool> LoadAsync([NotNull] ListAddAsyncCallback<ServerInformation> callback, [CanBeNull] IProgress<AsyncProgressEntry> progress,
                CancellationToken cancellation);
    }
}