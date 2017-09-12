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
            get => (string)GetValue(AppUpdateAvailableProperty);
            set => SetValue(AppUpdateAvailableProperty, value);
        }

        public static readonly DependencyProperty AppUpdateCommandProperty = DependencyProperty.Register(nameof(AppUpdateCommand), typeof(ICommand),
                typeof(AppModernWindow));

        public ICommand AppUpdateCommand {
            get => (ICommand)GetValue(AppUpdateCommandProperty);
            set => SetValue(AppUpdateCommandProperty, value);
        }

        public static readonly DependencyProperty AdditionalContentAvailableProperty = DependencyProperty.Register(nameof(AdditionalContentAvailable),
                typeof(bool), typeof(AppModernWindow));

        public bool AdditionalContentAvailable {
            get => GetValue(AdditionalContentAvailableProperty) as bool? == true;
            set => SetValue(AdditionalContentAvailableProperty, value);
        }

        public static readonly DependencyProperty AdditionalContentDownloadingProperty = DependencyProperty.Register(nameof(AdditionalContentDownloading),
                typeof(bool), typeof(AppModernWindow));

        public bool AdditionalContentDownloading {
            get => GetValue(AdditionalContentDownloadingProperty) as bool? == true;
            set => SetValue(AdditionalContentDownloadingProperty, value);
        }

        public static readonly DependencyProperty AdditionalContentToolTipProperty = DependencyProperty.Register(nameof(AdditionalContentToolTip), typeof(string),
                typeof(AppModernWindow));

        public string AdditionalContentToolTip {
            get => (string)GetValue(AdditionalContentToolTipProperty);
            set => SetValue(AdditionalContentToolTipProperty, value);
        }

        public static readonly DependencyProperty ShowAdditionalContentDialogCommandProperty = DependencyProperty.Register(nameof(ShowAdditionalContentDialogCommand),
                typeof(ICommand), typeof(AppModernWindow));

        public ICommand ShowAdditionalContentDialogCommand {
            get => (ICommand)GetValue(ShowAdditionalContentDialogCommandProperty);
            set => SetValue(ShowAdditionalContentDialogCommandProperty, value);
        }
    }
}