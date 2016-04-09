using System;
using System.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public static class ErrorMessage {
        public static void ShowWithoutLogging(string problemDescription, string solutionCommentary, Exception exception) {
            ModernDialog.ShowMessage((
                exception == null ? problemDescription :
                    $"{problemDescription}:\n\n[b][mono]{exception.Message}[/mono][/b]"
            ) + (solutionCommentary == null ? "" : "\n\n[i]" + solutionCommentary + "[/i]"), "Oops!", MessageBoxButton.OK);
        }

        public static void Show(string problemDescription, string solutionCommentary, Exception exception) {
            ShowWithoutLogging(problemDescription, solutionCommentary, exception);
            Logging.Warning(problemDescription + ":\n" + exception);
        }
    }
}
