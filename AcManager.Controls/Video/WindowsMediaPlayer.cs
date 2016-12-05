using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Video {
    public class WindowsMediaPlayer : IVideoPlayer {
        private readonly BooleanSwitch _switch;
        private readonly MediaElement _mediaPlayer;
        private bool _active;

        public WindowsMediaPlayer() {
            _mediaPlayer = new MediaElement {
                LoadedBehavior = MediaState.Manual
            };

            _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

            _switch = new BooleanSwitch {
                Value = true,
                True = _mediaPlayer,
                False = new TextBlock {
                    Text = "Can’t play file. Make sure Windows Media Player is available and required codec is installed, or enable VLC plugin instead.",
                    Width = 320,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        public UIElement Initialize() {
            return _switch;
        }

        public Uri Source {
            get { return _mediaPlayer.Source; }
            set {
                _switch.Value = true;
                _mediaPlayer.Source = value;

                if (_active) {
                    ResetAndPlay();
                } else {
                    _mediaPlayer.Stop();
                }
            }
        }

        public Stretch Stretch {
            get { return _mediaPlayer.Stretch; }
            set { _mediaPlayer.Stretch = value; }
        }

        public bool IsLooped { get; set; }

        public double Volume {
            get { return _mediaPlayer.Volume; }
            set { _mediaPlayer.Volume = value; }
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e) {
            Ended?.Invoke(this, EventArgs.Empty);
            if (_active && IsLooped) {
                ResetAndPlay();
            }
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e) {
            Logging.Warning(e.ErrorException);
            _switch.Value = false;
        }

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e) {
            if (_active) {
                _mediaPlayer.Play();
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ResetAndPlay() {
            _mediaPlayer.Position = TimeSpan.Zero;
            _mediaPlayer.Play();
        }

        public void Play() {
            _active = true;
            ResetAndPlay();

            if (_mediaPlayer.HasVideo) {
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Stop() {
            _active = false;
            _mediaPlayer.Pause();
        }

        public event EventHandler Started;
        public event EventHandler Ended;

        public void Dispose() {
            _mediaPlayer.Stop();
            _mediaPlayer.Source = null;
        }
    }
}