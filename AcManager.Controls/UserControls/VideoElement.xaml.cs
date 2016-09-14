using System;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Helpers;
using MediaState = xZune.Vlc.Interop.Media.MediaState;

namespace AcManager.Controls.UserControls {
    /// <summary>
    /// Interaction logic for VideoElement.xaml
    /// </summary>
    public partial class VideoElement {
        public VideoElement() {
            InitializeComponent();
        }

        public static readonly DependencyProperty StaticProperty = DependencyProperty.Register(nameof(Static), typeof(string),
                typeof(VideoElement));

        public string Static {
            get { return (string)GetValue(StaticProperty); }
            set { SetValue(StaticProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(string),
                typeof(VideoElement), new PropertyMetadata(OnSourceChanged));

        public string Source {
            get { return (string)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((VideoElement)o).OnSourceChanged();
        }

        private void OnSourceChanged() {
            if (_started) {
                Player.BeginStop(Stopped);
                HideVideo();
            }
        }

        private bool _loaded, _started, _playing;
        private async void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            await Task.Delay(2000);

            try {
                Logging.Write("Initialization: " + PluginsManager.Instance.GetPluginDirectory("VLC"));
                Player.Initialize(PluginsManager.Instance.GetPluginDirectory("VLC"), @"--ignore-config", @"--no-video-title", @"--no-sub-autodetect-file");
                Logging.Write("Player.BeginStop()");
                Player.BeginStop(Stopped);
                _started = true;
            } catch (Exception ex) {
                NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, ex);
            }
        }

        private void Stopped() {
            try {
                string source = null;
                Application.Current.Dispatcher.Invoke(() => {
                    source = Source;
                    Player.StateChanged += Player_StateChanged;
                });

                Logging.Write("Player.LoadMedia()");
                Player.LoadMedia(source);
                Logging.Write("Player.Play()");
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
            await Task.Delay(2000);
            VisualStateManager.GoToElementState(Player, @"Visible", true);
        }

        private void HideVideo() {
            VisualStateManager.GoToElementState(Player, @"Hidden", true);
        }
    }
}
