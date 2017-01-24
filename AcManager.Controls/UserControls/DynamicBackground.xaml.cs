using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AcManager.Controls.Video;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public partial class DynamicBackground : IDisposable {
        public static bool OptionUseVlc = false;

        [NotNull]
        private readonly VideoPlayer _player;

        public DynamicBackground() {
            InitializeComponent();
            _player = new VideoPlayer(!OptionUseVlc) {
                Stretch = Stretch.UniformToFill,
                IsLooped = true,
                Volume = 0d
            };
            VideoWrapper.Child = _player;
            _player.Started += OnStart;
        }

        private void OnStart(object sender, EventArgs e) {
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
                typeof(DynamicBackground), new PropertyMetadata(OnAnimatedChanged));

        [CanBeNull]
        public string Animated {
            get { return (string)GetValue(AnimatedProperty); }
            set { SetValue(AnimatedProperty, value); }
        }

        private static void OnAnimatedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DynamicBackground)o).OnAnimatedChanged((string)e.NewValue);
        }

        private async void OnAnimatedChanged([CanBeNull] string filename) {
            if (_started) {
                HideVideo();
                await Task.Delay(1000);
            }

            _player.Source = filename == null ? null : new Uri(filename, UriKind.Relative);
        }

        private bool _loaded, _started;
        private Window _window;

        // For cases when used in VisualBrush
        /*protected override Size MeasureOverride(Size constraint) {
            Initialize();
            return base.MeasureOverride(constraint);
        }*/

        private async void Initialize() {
            if (_loaded) return;
            _loaded = true;

            await Task.Delay(2000);

            try {
                _window = Window.GetWindow(this);

                if (_window == null) {
                    Logging.Warning("Can’t find window");
                    return;
                }

                _window.Activated += Window_Activated;
                _window.Deactivated += Window_Deactivated;

                if (_window.IsActive) {
                    _player.PlayRandomly();
                    _started = true;
                }
            } catch (Exception ex) {
                NonfatalError.NotifyBackground(ControlsStrings.VideoViewer_CannotPlay, ControlsStrings.VideoViewer_CannotPlay_Commentary, ex);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Initialize();
        }

        private void Window_Activated(object sender, EventArgs e) {
            _started = true;
            _player.PlayRandomly();
        }

        private async void Window_Deactivated(object sender, EventArgs e) {
            _started = false;
            HideVideo();
            await Task.Delay(1000);
            if (!_started) {
                _player.Stop();
            }
        }

        private async void ShowVideo() {
            await Task.Delay(300);
            VisualStateManager.GoToElementState(VideoWrapper, @"Visible", true);
        }

        private void HideVideo() {
            VisualStateManager.GoToElementState(VideoWrapper, @"Hidden", true);
        }
        public void Dispose() {
            _player.Dispose();
        }
    }
}
