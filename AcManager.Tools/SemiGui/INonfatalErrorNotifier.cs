using System;

namespace AcManager.Tools.SemiGui {
    /// <summary>
    /// Shows non-fatal errors to user. For example.
    /// </summary>
    public interface INonfatalErrorNotifier {
        void Notify(string problemDescription, string solutionCommentary, Exception exception);
    }
}