using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using xZune.Vlc.Wpf;
using MediaState = xZune.Vlc.Interop.Media.MediaState;

namespace AcManager.Controls.UserControls {
    public partial class DynamicBackground {
        public static bool OptionUseVlc = false;

        private interface IVideoPlayer {
            void Initialize(Decorator wrapper);

            void SetSource(Uri source);

            void Play();

            void Stop();

            event EventHandler Ready;
        }

        private class VlcPlayer : IVideoPlayer {
            [NotNull]
            private readonly xZune.Vlc.Wpf.VlcPlayer _vlcPlayer;

            [CanBeNull]
            private Uri _source;

            private bool _playing, _active;

            public VlcPlayer() {
                _vlcPlayer = new xZune.Vlc.Wpf.VlcPlayer {
                    AspectRatio = AspectRatio.Default,
                    Stretch = Stretch.UniformToFill,
                    EndBehavior = EndBehavior.Repeat,
                    IsHitTestVisible = false,
                    Volume = 0
                };

                _vlcPlayer.StateChanged += Player_StateChanged;
            }

            private void Stopped() {
                try {
                    _playing = false;

                    if (_source == null || !File.Exists(_source.OriginalString) || !_active) return;
                    // _vlcPlayer.LoadMediaWithOptions(source, ":avcodec-hw=dxva2");
                    _vlcPlayer.LoadMedia(_source.OriginalString);
                    _vlcPlayer.Play();
                } catch (Exception e) {
                    NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, e);
                }
            }

            public void Initialize(Decorator wrapper) {
                wrapper.Child = _vlcPlayer;
                _vlcPlayer.Initialize(PluginsManager.Instance.GetPluginDirectory("VLC"), @"--ignore-config", @"--no-video-title",
                        @"--no-sub-autodetect-file", @"--no-audio");
            }

            public void SetSource(Uri source) {
                _source = source;
                if (_active) {
                    _vlcPlayer.BeginStop(Stopped);
                }
            }

            public void Play() {
                _active = true;
                _vlcPlayer.BeginStop(Stopped);
            }

            public void Stop() {
                _active = false;
                _vlcPlayer.BeginStop(Stopped);
            }

            public event EventHandler Ready;

            private void Player_StateChanged(object sender, xZune.Vlc.ObjectEventArgs<MediaState> e) {
                if (e.Value == MediaState.Playing && !_playing) {
                    _playing = true;
                    Application.Current.Dispatcher.InvokeAsync(() => {
                        Ready?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }

        private class WindowsMediaPlayer : IVideoPlayer {
            [NotNull]
            private readonly MediaElement _mediaPlayer;
            private bool _active;

            public WindowsMediaPlayer() {
                _mediaPlayer = new MediaElement {
                    Stretch = Stretch.UniformToFill,
                    IsHitTestVisible = false,
                    LoadedBehavior = System.Windows.Controls.MediaState.Manual,
                    Volume = 0
                };
            }

            public void Initialize(Decorator wrapper) {
                _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
                wrapper.Child = _mediaPlayer;
            }

            private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e) {
                if (_active) {
                    _mediaPlayer.Play();
                    Ready?.Invoke(this, EventArgs.Empty);
                }
            }

            private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e) {
                if (_mediaPlayer.Source != null && _active) {
                    _mediaPlayer.Position = TimeSpan.Zero;
                    _mediaPlayer.Play();
                }
            }

            public void SetSource(Uri source) {
                _mediaPlayer.Source = source;
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            }

            public void Play() {
                _active = true;
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();

                if (_mediaPlayer.HasVideo) {
                    Ready?.Invoke(this, EventArgs.Empty);
                }
            }

            public void Stop() {
                _active = false;
                _mediaPlayer.Stop();
            }

            public event EventHandler Ready;
        }

        [NotNull]
        private readonly IVideoPlayer _player;

        public DynamicBackground() {
            InitializeComponent();
            _player = OptionUseVlc ? (IVideoPlayer)new VlcPlayer() : new WindowsMediaPlayer();
            _player.Ready += OnReady;
        }

        private void OnReady(object sender, EventArgs e) {
            ShowVideo();
        }

        public static readonly DependencyProperty StaticProperty = DependencyProperty.Register(nameof(Static), typeof(string),
                typeof(DynamicBackground));

        [CanBeNull]
        public string Static {
            get { return (string)GetValue(StaticProperty); }
            set { SetValue(StaticProperty, value); }
        }

        public static readonly DependencyProperty AnimatedProperty = DependencyProperty.Register(nameof(Animated), typeof(string),
                typeof(DynamicBackground), new PropertyMetadata(OnSourceChanged));

        [CanBeNull]
        public string Animated {
            get { return (string)GetValue(AnimatedProperty); }
            set { SetValue(AnimatedProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DynamicBackground)o).OnSourceChanged((string)e.NewValue);
        }

        private async void OnSourceChanged([CanBeNull] string filename) {
            if (_started) {
                HideVideo();
                await Task.Delay(1000);
            }

            _player.SetSource(filename != null && File.Exists(filename) ? new Uri(filename, UriKind.Relative) : null);
        }

        private bool _loaded, _started;
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

                _player.Initialize(VideoWrapper);
                ShowVideo();

                _started = true;
            } catch (Exception ex) {
                NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, ex);
            }
        }

        private void Window_Activated(object sender, EventArgs e) {
            _player.Play();
        }

        private async void Window_Deactivated(object sender, EventArgs e) {
            HideVideo();
            await Task.Delay(1000);
            _player.Stop();
        }

        private async void ShowVideo() {
            await Task.Delay(300);
            VisualStateManager.GoToElementState(VideoWrapper, @"Visible", true);
        }

        private void HideVideo() {
            VisualStateManager.GoToElementState(VideoWrapper, @"Hidden", true);
        }
    }
}
