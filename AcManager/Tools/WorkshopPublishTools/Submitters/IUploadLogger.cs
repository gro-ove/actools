using System;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public interface IUploadLogger : IProgress<AsyncProgressEntry> {
        IUploadLoggedOperation Begin(string message);

        void ReportProgress(string progress);

        IProgress<AsyncProgressEntry> Progress(string trimStart);

        IUploadLoggedParallelOperation BeginParallel(string message, string trimStart = null);

        void Write(string message);

        void Error(string message);
    }
}