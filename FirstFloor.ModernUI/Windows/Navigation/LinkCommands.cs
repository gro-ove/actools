using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Navigation {
    public static class LinkCommands {
        public static RoutedUICommand NavigateLink { get; } = new RoutedUICommand(UiStrings.NavigateLink, "NavigateLink", typeof(LinkCommands));

        // Dunno what I’m doing here, to be honest… RoutedUICommand? What the hell is that?
        public static DelegateCommand<object> NavigateLinkMainWindow { get; } = new DelegateCommand<object>(uri => {
            if (uri != null) {
                var s = uri.ToString();
                if (s.StartsWith("http://") || s.StartsWith("https://")) {
                    try {
                        Process.Start(s);
                    } catch (Exception) {
                        NonfatalError.Notify("Can’t open link", $"App tried to open: “{s}”");
                    }
                    return;
                }

                var mainWindow = Application.Current?.MainWindow;
                var u = new Uri(s, UriKind.RelativeOrAbsolute);

                if (mainWindow?.IsActive == true) {
                    var frame = mainWindow.FindVisualChild<ModernFrame>();
                    if (frame != null) {
                        BbCodeBlock.DefaultLinkNavigator.Navigate(u, frame);
                    }
                } else {
                    var dlg = new ModernDialog {
                        Title = "",
                        Content = new ModernFrame {
                            Source = u
                        },
                        MinHeight = 0,
                        MinWidth = 0,
                        Height = 540,
                        MaxHeight = 540,
                        SizeToContent = SizeToContent.Manual,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Width = 800,
                        MaxWidth = 800
                    };

                    dlg.Buttons = new[]{ dlg.OkButton };
                    dlg.ShowDialog();
                }
            }
        });
    }
}
