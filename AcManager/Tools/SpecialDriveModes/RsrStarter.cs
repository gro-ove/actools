using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Controls.Pages.Dialogs;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.SpecialDriveModes {
    public class RsrStarter {
        private readonly int _eventId;

        public RsrStarter(int eventId) {
            _eventId = eventId;
        }

        public async Task Start(IProgress<string> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report("Getting information about the event…");


        }

        public void Start() {
            using (var waiting = new WaitingDialog()) {
                var cancellation = waiting.CancellationToken;
                waiting.Report("");
            }
        }
    }
}
