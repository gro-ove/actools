using System;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Pages.Workshop {
    public interface IUploadLoggedParallelOperation : IDisposable, IProgress<AsyncProgressEntry> {
        void SetResult(string message);

        void SetFailed();
    }
}