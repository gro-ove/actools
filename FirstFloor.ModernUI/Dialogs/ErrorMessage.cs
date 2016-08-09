using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace FirstFloor.ModernUI.Dialogs {
    public static class ErrorMessage {
        public static void Show(NonfatalErrorEntry entry) {
            var text = (entry.Exception == null ? $"{entry.DisplayName}." : $"{entry.DisplayName}:\n\n[b][mono]{entry.Exception.Message}[/mono][/b]") +
                    (entry.Commentary == null ? "" : $"\n\n[i]{entry.Commentary}[/i]");
            var dlg = new ModernDialog {
                Title = UiStrings.Common_Oops,
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

            entry.Unseen = false;
        }
    }
}
