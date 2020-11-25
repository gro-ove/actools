using System;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public interface IUploadLoggedOperation : IDisposable {
        void SetResult(string message);

        void SetFailed();
    }
}