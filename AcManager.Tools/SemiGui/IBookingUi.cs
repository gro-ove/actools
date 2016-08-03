using System;
using System.Threading;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Managers.Online;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public interface IBookingUi : IDisposable {
        void Show([NotNull] ServerEntry server);

        void OnUpdate([CanBeNull] BookingResult response);

        CancellationToken CancellationToken { get; }
    }
}