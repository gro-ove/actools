using System;
using System.Windows.Controls;
using System.Windows.Input;
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
            Buttons = new Button[] { };
        }

        public class ViewModel : NotifyPropertyChanged {
            public string Filename { get; set; }

            public ViewModel([NotNull] string filename) {
                Filename = filename;
            }
        }

        private void VideoViewer_OnClosed(object sender, EventArgs e) {
            Player.Dispose();
        }

        private void ImageViewer_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                Close();
            }
        }

        private void ImageViewer_OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            }
        }

        private void CloseButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            Close();
        }

        private void Player_OnEnded(object sender, EventArgs e) {
            Close();
        }
    }
}
