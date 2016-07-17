using System;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    /// <summary>
    /// Shows non-fatal errors to user. For example.
    /// </summary>
    public interface INonfatalErrorNotifier {
        void Notify([LocalizationRequired] string problemDescription, [LocalizationRequired] string solutionCommentary, Exception exception);
    }
}