using System;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public static class ErrorMessage {
        public static void ShowWithoutLogging(string problemDescription, string solutionCommentary, Exception exception) {
            var text = (exception == null ? problemDescription + "." : $"{problemDescription}:\n\n[b][mono]{exception.Message}[/mono][/b]") +
                    (solutionCommentary == null ? "" : "\n\n[i]" + solutionCommentary + "[/i]");
            var dlg = new ModernDialog {
                Title = "Oops!",
                Content = new ScrollViewer {
                    Content = new BbCodeBlock { BbCode = text, Margin = new Thickness(0, 0, 0, 8) },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            dlg.Buttons = new[] { dlg.OkButton };
            dlg.Show();
        }

        public static void Show(string problemDescription, string solutionCommentary, Exception exception) {
            ShowWithoutLogging(problemDescription, solutionCommentary, exception);
            Logging.Warning(problemDescription + ":\n" + exception);
        }
    }
}
