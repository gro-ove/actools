using System;

namespace AcManager.Pages.Workshop {
    public interface IUploadLoggedOperation : IDisposable {
        void SetResult(string message);

        void SetFailed();
    }
}