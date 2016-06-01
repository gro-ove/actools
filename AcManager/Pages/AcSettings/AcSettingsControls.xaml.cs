using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettingsControls;
using AcManager.Tools.Helpers.DirectInput;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls {
        public AcSettingsControls() {
            InitializeComponent();
            DataContext = new AcControlsViewModel();
            ResizingStuff();
        }

        private void ResizingStuff() {
            DetectedControllers.Visibility = ActualWidth > 600 ? Visibility.Visible : Visibility.Collapsed;
        }

        public class AcControlsViewModel : NotifyPropertyChanged {
            internal AcControlsViewModel() { }

            public AcSettingsHolder.ControlsSettings Controls => AcSettingsHolder.Controls;
        }

        private void AcSettingsControls_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizingStuff();
        }

        public static IValueConverter ModeToUriConverter { get; } = new ModeToUriConverterInner();

        private class ModeToUriConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                switch (value?.ToString()) {
                    case "WHEEL":
                        return new Uri("/Pages/AcSettings/AcSettingsControls_Wheel.xaml", UriKind.Relative);

                    case "X360":
                        // TODO
                        return new Uri("/Pages/Miscellaneous/WorkInProgress.xaml?xbox360", UriKind.Relative);

                    case "KEYBOARD":
                        // TODO
                        return new Uri("/Pages/Miscellaneous/WorkInProgress.xaml?keyboard", UriKind.Relative);

                    default:
                        return null;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        private bool _loaded;

        private void AcSettingsControls_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            AcSettingsHolder.Controls.Used++;
        }

        private void AcSettingsControls_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            AcSettingsHolder.Controls.Used--;
        }

        private void AcSettingsControls_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                case Key.Back: {
                        var waiting = AcSettingsHolder.Controls.GetWaiting();
                        if (waiting != null) {
                            waiting.WaitingFor = WaitingFor.None;
                            e.Handled = true;
                        }

                        break;
                    }

                case Key.Delete: {
                        var waiting = AcSettingsHolder.Controls.GetWaiting();
                        if (waiting != null) {
                            if (waiting.WaitingFor == WaitingFor.Wheel) {
                                waiting.Clear();
                            } else {
                                waiting.ClearKeyboard();
                            }
                            waiting.WaitingFor = WaitingFor.None;
                            e.Handled = true;
                        }

                        break;
                    }
            }

            if (e.Key.IsInputAssignable()) {
                var waiting = AcSettingsHolder.Controls.GetWaiting(WaitingFor.Keyboard) as WheelButtonEntry;
                if (waiting != null) {
                    waiting.WaitingFor = WaitingFor.None;
                    waiting.SelectedKeyboardButton = AcSettingsHolder.Controls.GetKeyboardInputButton(KeyInterop.VirtualKeyFromKey(e.Key));
                    e.Handled = true;
                }
            }
        }
    }
}
