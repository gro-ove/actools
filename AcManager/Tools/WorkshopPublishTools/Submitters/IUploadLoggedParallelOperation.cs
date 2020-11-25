using System;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public interface IUploadLoggedParallelOperation : IDisposable, IProgress<AsyncProgressEntry> {
        void SetResult(string message);

        void SetFailed();
    }
}