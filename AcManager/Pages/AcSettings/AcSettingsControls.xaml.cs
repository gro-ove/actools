using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls : IAcControlsConflictResolver {
        public AcSettingsControls() {
            AcSettingsHolder.Controls.ConflictResolver = this;

            InitializeComponent();
            DataContext = new AcControlsViewModel();
            ResizingStuff();
        }

        private void ResizingStuff() {
            DetectedControllers.Visibility = ActualWidth > 720 ? Visibility.Visible : Visibility.Collapsed;
        }

        public class AcControlsViewModel : NotifyPropertyChanged {
            internal AcControlsViewModel() { }

            public AcSettingsHolder.ControlsSettings Controls => AcSettingsHolder.Controls;

            public AcSettingsHolder.SystemSettings System => AcSettingsHolder.System;
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
                        return new Uri("/Pages/AcSettings/AcSettingsControls_Keyboard.xaml", UriKind.Relative);

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
                case Key.Back:
                case Key.Enter:
                    if (AcSettingsHolder.Controls.StopWaiting()) {
                        e.Handled = true;
                    }
                    break;

                case Key.Delete:
                    if (AcSettingsHolder.Controls.ClearWaiting()) {
                        e.Handled = true;
                    }
                    break;

                default:
                    if (AcSettingsHolder.Controls.AssignKey(e.Key)) {
                        e.Handled = true;
                    }
                    break;
            }
        }

        public AcControlsConflictSolution Resolve(string inputDisplayName, IEnumerable<string> existingAssignments) {
            var list = existingAssignments.Select(x => $"“{x}”").ToList();
            var message = list.Count > 1
                    ? $"“{inputDisplayName}” is already used for {list.SkipLast(1).JoinToString(", ")} and {list.Last()}. Do you want to remove old usings first?"
                    : $"“{inputDisplayName}” is already used for {list.First()}. Do you want to remove old using first?";

            var dlg = new ModernDialog {
                Title = "Already used",
                Content = new ScrollViewer {
                    Content = new BbCodeBlock { BbCode = message, Margin = new Thickness(0, 0, 0, 8) },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            dlg.Buttons = new[] {
                dlg.CreateCloseDialogButton("Yes, Remove Old", true, false, MessageBoxResult.Yes),
                dlg.CreateCloseDialogButton("No, Apply to All", false, false, MessageBoxResult.No),
                dlg.CreateCloseDialogButton("Flip Usings", false, false, MessageBoxResult.OK),
                dlg.CreateCloseDialogButton("Cancel", false, true, MessageBoxResult.Cancel),
            };
            dlg.ShowDialog();

            switch (dlg.MessageBoxResult) {
                case MessageBoxResult.Yes:
                    return AcControlsConflictSolution.ClearPrevious;
                case MessageBoxResult.No:
                    return AcControlsConflictSolution.KeepEverything;
                case MessageBoxResult.OK:
                    return AcControlsConflictSolution.Flip;
                case MessageBoxResult.Cancel:
                    return AcControlsConflictSolution.Cancel;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
