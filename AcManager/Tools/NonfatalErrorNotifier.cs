using System;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Tools.SemiGui;

namespace AcManager.Tools {
    public class NonfatalErrorNotifier : INonfatalErrorNotifier { 
        void INonfatalErrorNotifier.Notify(string problemDescription, string solutionCommentary, Exception exception) {
            Application.Current.Dispatcher.Invoke(() => {
                ErrorMessage.Show(problemDescription, solutionCommentary, exception);
            });
        }
    }
}