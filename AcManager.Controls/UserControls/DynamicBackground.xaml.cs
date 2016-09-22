using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Helpers;
using MediaState = xZune.Vlc.Interop.Media.MediaState;

namespace AcManager.Controls.UserControls {
    /// <summary>
    /// Interaction logic for VideoElement.xaml
    /// </summary>
    public partial class DynamicBackground {
        public DynamicBackground() {
            InitializeComponent();
        }

        public static readonly DependencyProperty StaticProperty = DependencyProperty.Register(nameof(Static), typeof(string),
                typeof(DynamicBackground));

        public string Static {
            get { return (string)GetValue(StaticProperty); }
            set { SetValue(StaticProperty, value); }
        }

        public static readonly DependencyProperty AnimatedProperty = DependencyProperty.Register(nameof(Animated), typeof(string),
                typeof(DynamicBackground), new PropertyMetadata(OnSourceChanged));

        public string Animated {
            get { return (string)GetValue(AnimatedProperty); }
            set { SetValue(AnimatedProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DynamicBackground)o).OnSourceChanged();
        }

        private async void OnSourceChanged() {
            if (_started) {
                HideVideo();

                await Task.Delay(1000);
                Player.BeginStop(Stopped);
            }
        }

        private bool _loaded, _started, _playing;
        private Window _window;

        private async void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            await Task.Delay(2000);

            try {
                _window = Window.GetWindow(this);
                if (_window == null) return;

                _window.Activated += Window_Activated;
                _window.Deactivated += Window_Deactivated;

                Player.StateChanged += Player_StateChanged;

                Player.Initialize(PluginsManager.Instance.GetPluginDirectory("VLC"), @"--ignore-config", @"--no-video-title", @"--no-sub-autodetect-file", @"--no-audio");
                Player.BeginStop(Stopped);
                _started = true;
            } catch (Exception ex) {
                NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, ex);
            }
        }

        private void Window_Activated(object sender, EventArgs e) {
            Player.BeginStop(Stopped);
        }

        private async void Window_Deactivated(object sender, EventArgs e) {
            HideVideo();

            await Task.Delay(1000);
            Player.BeginStop(Stopped);
        }

        private void Stopped() {
            try {
                string source = null;
                Application.Current.Dispatcher.Invoke(() => {
                    if (!_window.IsActive) return;
                    source = Animated;
                });

                if (source == null || !File.Exists(source)) return;
                _playing = false;
               //  Player.LoadMediaWithOptions(source, ":avcodec-hw=dxva2");
                Player.LoadMedia(source);
                Player.Play();
            } catch (Exception e) {
                NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, e);
            }
        }

        private void Player_StateChanged(object sender, xZune.Vlc.ObjectEventArgs<MediaState> e) {
            if (e.Value == MediaState.Playing && !_playing) {
                _playing = true;
                Application.Current.Dispatcher.InvokeAsync(ShowVideo);
            }
        }

        private async void ShowVideo() {
            await Task.Delay(1000);
            VisualStateManager.GoToElementState(Player, @"Visible", true);
        }

        private void HideVideo() {
            VisualStateManager.GoToElementState(Player, @"Hidden", true);
        }
    }
}
