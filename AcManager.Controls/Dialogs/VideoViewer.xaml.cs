using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Presentation;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Path = System.IO.Path;

namespace AcManager.Controls.Dialogs {
    public partial class VideoViewer {
        public VideoViewer([NotNull] string filename, string title = null) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));

            Title = title ?? Path.GetFileName(filename);
            DataContext = new ViewModel(filename);
            InitializeComponent();
            Owner = null;
            Buttons = new Button[] { };

            if (AppAppearanceManager.Instance.BlurImageViewerBackground) {
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                BlurBackground = true;
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            public string Filename { get; set; }

            public ViewModel([NotNull] string filename) {
                Filename = filename;
            }
        }

        private void OnClosed(object sender, EventArgs e) {
            Player.Dispose();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                Close();
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs routedEventArgs) {
            Close();
        }

        private void OnEnded(object sender, EventArgs e) {
            Close();
        }
    }
}
