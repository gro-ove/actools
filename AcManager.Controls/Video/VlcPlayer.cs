using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using xZune.Vlc.Wpf;
using MediaState = xZune.Vlc.Interop.Media.MediaState;

namespace AcManager.Controls.Video {
    public class VlcPlayer : IVideoPlayer {
        public static bool IsSupported() {
            return PluginsManager.Instance.IsPluginEnabled("VLC");
        }

        [NotNull]
        private readonly xZune.Vlc.Wpf.VlcPlayer _vlcPlayer;

        [CanBeNull]
        private Uri _source;

        private bool _playing, _active;

        public VlcPlayer() {
            _vlcPlayer = new xZune.Vlc.Wpf.VlcPlayer {
                AspectRatio = AspectRatio.Default
            };

            _vlcPlayer.StateChanged += Player_StateChanged;
        }

        private void Stopped() {
            try {
                _playing = false;

                if (_source == null || !File.Exists(_source.OriginalString) || !_active)
                    return;
                // _vlcPlayer.LoadMediaWithOptions(source, ":avcodec-hw=dxva2");
                _vlcPlayer.LoadMedia(_source.OriginalString);
                _vlcPlayer.Play();
            } catch (Exception e) {
                NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, e);
            }
        }

        public UIElement Initialize() {
            try {
                _vlcPlayer.Initialize(PluginsManager.Instance.GetPluginDirectory("VLC"), @"--ignore-config", @"--no-video-title", @"--no-sub-autodetect-file");
            } catch (Exception ex) {
                NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, ex);
            }
            return _vlcPlayer;
        }

        public Uri Source {
            get { return _source; }
            set {
                _source = value;
                if (_active) {
                    _vlcPlayer.BeginStop(Stopped);
                }
            }
        }

        public Stretch Stretch {
            get { return _vlcPlayer.Stretch; }
            set { _vlcPlayer.Stretch = value; }
        }

        public bool IsLooped {
            get { return _vlcPlayer.EndBehavior == EndBehavior.Repeat; }
            set { _vlcPlayer.EndBehavior = value ? EndBehavior.Repeat : EndBehavior.Nothing; }
        }

        public double Volume {
            get { return 0.01 * _vlcPlayer.Volume; }
            set { _vlcPlayer.Volume = (int)(100 * value); }
        }

        public void Play() {
            _active = true;
            _vlcPlayer.BeginStop(Stopped);
        }

        public void Stop() {
            _active = false;
            _vlcPlayer.BeginStop(Stopped);
        }

        public event EventHandler Started;
        public event EventHandler Ended;

        private void Player_StateChanged(object sender, xZune.Vlc.ObjectEventArgs<MediaState> e) {
            if (e.Value == MediaState.Playing && !_playing) {
                _playing = true;
                Application.Current.Dispatcher.InvokeAsync(() => {
                    Started?.Invoke(this, EventArgs.Empty);
                });
            } else if (e.Value == MediaState.Ended && _playing) {
                Application.Current.Dispatcher.InvokeAsync(() => {
                    Ended?.Invoke(this, EventArgs.Empty);
                });
            }
        }

        public void Dispose() {
            _vlcPlayer.Dispose();
        }
    }
}