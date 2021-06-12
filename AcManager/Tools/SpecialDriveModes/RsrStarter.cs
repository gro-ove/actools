using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api.Rsr;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Tools.SpecialDriveModes {
    public class RsrStarter {
        private readonly int _eventId;

        public RsrStarter(int eventId) {
            _eventId = eventId;
        }

        public async Task Start(IProgress<string> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AppStrings.RsrStarter_GettingInformation);
            var entry = await RsrApiProvider.GetEventInformationAsync(_eventId, cancellation);
            Logging.Write("Car ID: " + entry?.CarId);
        }

        public void StartWithDialog() {
            using (var waiting = new WaitingDialog()) {
                Start(waiting, waiting.CancellationToken).Ignore();
            }
        }
    }
}
