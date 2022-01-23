using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AcTools.Utils;
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
                False = new BbCodeBlock {
                    Width = 320,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private void UpdateErrorMessage(string filename) {
            string url;
            var extension = Path.GetExtension(filename)?.Replace(@".", "").ToLowerInvariant();
            switch (extension) {
                case "ogv":
                    url = @"https://xiph.org/dshow/";
                    break;

                case "webm":
                    url = @"https://tools.google.com/dlpage/webmmf/";
                    break;

                case null:
                case "":
                    url = null;
                    break;

                default:
                    url = $@"https://google.com/search?q={extension}+windows+media+player+codec";
                    break;
            }

            ((BbCodeBlock)_switch.False).Text = string.Format(
                    "Can’t play file. Make sure Windows Media Player is available and {0}required codec is installed{1}, or enable VLC plugin instead.",
                    url == null ? "" : $@"[url={BbCodeBlock.EncodeAttribute(url)}]",
                    url == null ? "" : @"[/url]");
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
            UpdateErrorMessage(Source?.OriginalString);
            _switch.Value = false;
        }

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e) {
            if (_active) {
                _mediaPlayer.Play();
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ResetAndPlay(TimeSpan position = default) {
            _mediaPlayer.Position = position;
            _mediaPlayer.Play();
        }

        public void Play() {
            _active = true;
            ResetAndPlay(_paused);

            if (_mediaPlayer.HasVideo) {
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        public void PlayRandomly() {
            _active = true;

            var duration = _mediaPlayer.NaturalDuration;
            ResetAndPlay(duration.HasTimeSpan ?
                TimeSpan.FromSeconds(MathUtils.Random() * Math.Max(_mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds - 5d, 0d)) :
                TimeSpan.Zero);

            if (_mediaPlayer.HasVideo) {
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        private TimeSpan _paused;

        public void Pause() {
            _paused = _mediaPlayer.Position;
            _active = false;
            _mediaPlayer.Pause();
        }

        public void Stop() {
            _paused = TimeSpan.Zero;
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