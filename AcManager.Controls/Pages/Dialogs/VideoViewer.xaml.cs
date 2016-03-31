using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Addons;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Helpers;
using MediaState = xZune.Vlc.Interop.Media.MediaState;
using Path = System.IO.Path;

namespace AcManager.Controls.Pages.Dialogs {
    public partial class VideoViewer {
        public string Filename { get; }

        public VideoViewer(string filename, string title = null) {
            DataContext = this;
            Filename = filename;
            Title = title ?? Path.GetFileName(filename);
            InitializeComponent();
            Buttons = new Button[] { };
        }

        private bool _loaded;
        private void VideoViewer_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            try {
                Logging.Write("[VLC] Initialization: " + AppAddonsManager.Instance.GetAddonDirectory("VLC"));
                Player.Initialize(AppAddonsManager.Instance.GetAddonDirectory("VLC"), "--ignore-config", "--no-video-title", "--no-sub-autodetect-file");
                Logging.Write("[VLC] Player.BeginStop()");
                Player.BeginStop(Stopped);
            } catch (Exception ex) {
                NonfatalError.Notify("Can't play video", "Make sure VLC addon is installed properly.", ex);
                Close();
            }
        }

        private void Stopped() {
            try {
                Logging.Write("[VLC] Player.LoadMedia()");
                Player.LoadMedia(Filename);
                Logging.Write("[VLC] Player.Play()");
                Player.Play();
                Player.StateChanged += Player_StateChanged;
            } catch (Exception e) {
                NonfatalError.Notify("Can't play video", "Make sure VLC addon is installed properly.", e);
                Close();
            }
        }

        private void Player_StateChanged(object sender, xZune.Vlc.ObjectEventArgs<xZune.Vlc.Interop.Media.MediaState> e) {
            if (Player.State == MediaState.Ended) {
                Application.Current.Dispatcher.Invoke(Close);
            }
        }

        private void VideoViewer_OnClosed(object sender, EventArgs e) {
            try {
                Player.Dispose();
                Logging.Write("[VLC] Disposed");
            } catch (Exception ex) {
                Logging.Write("[VLC] Dispose exception: " + ex);
            }
        }

        public static bool IsSupported() {
            return AppAddonsManager.Instance.IsAddonEnabled("VLC");
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
    }
}
