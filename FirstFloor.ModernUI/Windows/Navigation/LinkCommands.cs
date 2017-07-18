using System;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Navigation {
    public static class LinkCommands {
        public static RoutedUICommand NavigateLink { get; } = new RoutedUICommand(UiStrings.NavigateLink, "NavigateLink", typeof(LinkCommands));

        // Dunno what I’m doing here, tbh… RoutedUICommand? What the hell is that?
        public static DelegateCommand<object> NavigateMainWindow { get; } = new DelegateCommand<object>(uri => {
            if (uri != null) {
                var mainWindow = Application.Current?.MainWindow;
                var u = new Uri(uri.ToString(), UriKind.RelativeOrAbsolute);

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
