using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls {
    [ContentProperty(nameof(AdditionalContent))]
    public class AppModernWindow : ModernWindow {
        public AppModernWindow() {
            DefaultStyleKey = typeof(AppModernWindow);
        }

        public static readonly DependencyProperty AppUpdateAvailableProperty = DependencyProperty.Register(nameof(AppUpdateAvailable), typeof(string),
                typeof(AppModernWindow));

        public string AppUpdateAvailable {
            get { return (string)GetValue(AppUpdateAvailableProperty); }
            set { SetValue(AppUpdateAvailableProperty, value); }
        }

        public static readonly DependencyProperty AppUpdateCommandProperty = DependencyProperty.Register(nameof(AppUpdateCommand), typeof(ICommand),
                typeof(AppModernWindow));

        public ICommand AppUpdateCommand {
            get { return (ICommand)GetValue(AppUpdateCommandProperty); }
            set { SetValue(AppUpdateCommandProperty, value); }
        }

        public static readonly DependencyProperty AdditionalContentAvailableProperty = DependencyProperty.Register(nameof(AdditionalContentAvailable),
                typeof(bool), typeof(AppModernWindow));

        public bool AdditionalContentAvailable {
            get { return (bool)GetValue(AdditionalContentAvailableProperty); }
            set { SetValue(AdditionalContentAvailableProperty, value); }
        }

        public static readonly DependencyProperty AdditionalContentDownloadingProperty = DependencyProperty.Register(nameof(AdditionalContentDownloading),
                typeof(bool), typeof(AppModernWindow));

        public bool AdditionalContentDownloading {
            get { return (bool)GetValue(AdditionalContentDownloadingProperty); }
            set { SetValue(AdditionalContentDownloadingProperty, value); }
        }

        public static readonly DependencyProperty AdditionalContentToolTipProperty = DependencyProperty.Register(nameof(AdditionalContentToolTip), typeof(string),
                typeof(AppModernWindow));

        public string AdditionalContentToolTip {
            get { return (string)GetValue(AdditionalContentToolTipProperty); }
            set { SetValue(AdditionalContentToolTipProperty, value); }
        }

        public static readonly DependencyProperty ShowAdditionalContentDialogCommandProperty = DependencyProperty.Register(nameof(ShowAdditionalContentDialogCommand),
                typeof(ICommand), typeof(AppModernWindow));

        public ICommand ShowAdditionalContentDialogCommand {
            get { return (ICommand)GetValue(ShowAdditionalContentDialogCommandProperty); }
            set { SetValue(ShowAdditionalContentDialogCommandProperty, value); }
        }
    }
}