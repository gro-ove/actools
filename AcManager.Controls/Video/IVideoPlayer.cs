using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AcManager.Tools.Managers.Plugins;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.Video {
    public class VideoPlayer : Border, IDisposable {
        private readonly IVideoPlayer _player;

        public VideoPlayer(bool useSystemPlayer) {
            _player = !useSystemPlayer && PluginsManager.Instance.IsPluginEnabled("VLC") ? (IVideoPlayer)new VlcPlayer() : new WindowsMediaPlayer();

            _player.Stretch = Stretch;
            _player.IsLooped = IsLooped;
            _player.Volume = Volume;

            _player.Started += Player_Start;
            _player.Ended += Player_End;

            Loaded += (sender, args) => {
                if (AutoPlay) {
                    _player.Play();
                }
            };
        }

        public VideoPlayer() : this(false) {}

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            base.OnVisualParentChanged(oldParent);
            Initialize();
        }

        private void Initialize() {
            if (Child == null) {
                Child = _player.Initialize();
            }
        }

        public event EventHandler Started;

        public event EventHandler Ended;

        private void Player_Start(object sender, EventArgs e) {
            Started?.Invoke(this, e);
        }

        private void Player_End(object sender, EventArgs e) {
            Ended?.Invoke(this, e);
        }

        public void Play() {
            Initialize();
            _player.Play();
        }

        public void PlayRandomly() {
            Initialize();
            _player.PlayRandomly();
        }

        public void Stop() {
            _player.Stop();
        }

        public void Pause() {
            _player.Pause();
        }

        public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register(nameof(AutoPlay), typeof(bool),
                typeof(VideoPlayer));

        public bool AutoPlay {
            get { return (bool)GetValue(AutoPlayProperty); }
            set { SetValue(AutoPlayProperty, value); }
        }

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(VideoPlayer),
                new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

        public Stretch Stretch {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        private static void OnStretchChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((VideoPlayer)o).OnStretchChanged((Stretch)e.NewValue);
        }

        private void OnStretchChanged(Stretch newValue) {
            _player.Stretch = newValue;
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(Uri), typeof(VideoPlayer),
                new PropertyMetadata(OnSourceChanged));

        public Uri Source {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((VideoPlayer)o).OnSourceChanged((Uri)e.NewValue);
        }

        private void OnSourceChanged(Uri newValue) {
            _player.Source = newValue == null || !File.Exists(newValue.OriginalString) ? null : newValue;
        }

        public static readonly DependencyProperty IsLoopedProperty = DependencyProperty.Register(nameof(IsLooped), typeof(bool), typeof(VideoPlayer),
                new PropertyMetadata(false, OnIsLoopedChanged));

        public bool IsLooped {
            get { return (bool)GetValue(IsLoopedProperty); }
            set { SetValue(IsLoopedProperty, value); }
        }

        private static void OnIsLoopedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((VideoPlayer)o).OnIsLoopedChanged((bool)e.NewValue);
        }

        private void OnIsLoopedChanged(bool newValue) {
            _player.IsLooped = newValue;
        }

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(nameof(Volume), typeof(double), typeof(VideoPlayer),
                new PropertyMetadata(1d, OnVolumeChanged));

        public double Volume {
            get { return (double)GetValue(VolumeProperty); }
            set { SetValue(VolumeProperty, value); }
        }

        private static void OnVolumeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((VideoPlayer)o).OnVolumeChanged((double)e.NewValue);
        }

        private void OnVolumeChanged(double newValue) {
            _player.Volume = newValue;
        }
        public void Dispose() {
            try {
                _player.Dispose();
            } catch (Exception ex) {
                Logging.Write(ex);
            }
        }
    }

    internal interface IVideoPlayer : IDisposable {
        UIElement Initialize();

        Uri Source { get; set; }

        Stretch Stretch { get; set; }

        bool IsLooped { get; set; }

        double Volume { get; set; }

        void Play();

        void PlayRandomly();

        void Pause();

        void Stop();

        event EventHandler Started;

        event EventHandler Ended;
    }
}