// #define ENABLED

using System.Windows;
using System.Windows.Controls.Primitives;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BetterScrollBar : ScrollBar {

        public static readonly DependencyProperty MinThumbLengthProperty = DependencyProperty.Register(nameof(MinThumbLength), typeof(double),
                typeof(BetterScrollBar), new UIPropertyMetadata(40d, OnMinThumbLengthPropertyChanged));

        public double MinThumbLength {
            get => GetValue(MinThumbLengthProperty) as double? ?? 0d;
            set => SetValue(MinThumbLengthProperty, value);
        }

        private static void OnMinThumbLengthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
#if ENABLED
            BetterScrollBar instance = d as BetterScrollBar;
            instance?.OnMinThumbLengthChanged();
#endif
        }

#if ENABLED
        private double? _originalViewportSize;

        public BetterScrollBar() {
            SizeChanged += OnSizeChanged;
        }

        private bool _trackSubscribed;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            SubscribeTrack();
        }

        private void SubscribeTrack() {
            if (_trackSubscribed || Track == null) return;
            Track.SizeChanged += OnTrackSizeChanged;
            _trackSubscribed = true;
        }

        private void OnTrackSizeChanged(object sender, SizeChangedEventArgs e) {
            OnMinThumbLengthChanged();
        }

        protected override void OnMaximumChanged(double oldMaximum, double newMaximum) {
            base.OnMaximumChanged(oldMaximum, newMaximum);
            OnMinThumbLengthChanged();
        }

        protected override void OnMinimumChanged(double oldMinimum, double newMinimum) {
            base.OnMinimumChanged(oldMinimum, newMinimum);
            OnMinThumbLengthChanged();
        }

        private void OnMinThumbLengthChanged() {
            SubscribeTrack();
            UpdateViewPort();
        }

        private void UpdateViewPort() {
            if (Track == null) return;
            if (_originalViewportSize == null) {
                _originalViewportSize = ViewportSize;
            }

            var trackLength = Orientation == Orientation.Vertical ? Track.ActualHeight : Track.ActualWidth;
            var thumbHeight = _originalViewportSize.Value / (Maximum - Minimum + _originalViewportSize.Value) * trackLength;
            if (thumbHeight < MinThumbLength && !double.IsNaN(thumbHeight)) {
                ViewportSize = MinThumbLength * (Maximum - Minimum) / (trackLength + MinThumbLength);
            }
        }
#endif
    }
}