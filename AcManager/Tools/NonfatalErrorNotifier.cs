using System;
using AcManager.Controls.Helpers;
using AcManager.Tools.SemiGui;

namespace AcManager.Tools.AcErrors {
    public class NonfatalErrorNotifier : INonfatalErrorNotifier { 

        void INonfatalErrorNotifier.Notify(string problemDescription, string solutionCommentary, Exception exception) {
            ErrorMessage.Show(problemDescription, solutionCommentary, exception);
        }
    }
}