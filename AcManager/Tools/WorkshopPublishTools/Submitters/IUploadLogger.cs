namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public interface IUploadLogger {
        IUploadLoggedOperation Begin(string message);

        IUploadLoggedParallelOperation BeginParallel(string message, string trimStart = null);

        void Write(string message);

        void Error(string message);
    }
}