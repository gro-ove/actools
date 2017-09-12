using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class LabeledToggleButton : ToggleButton {
        public LabeledToggleButton() {
            DefaultStyleKey = typeof(LabeledToggleButton);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private bool _loaded;

        private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs) {
            if (!_loaded) return;
            _loaded = false;
            CompositionTargetEx.Rendering -= OnRendering;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            CompositionTargetEx.Rendering += OnRendering;
        }

        public static readonly DependencyProperty HighlightCheckedProperty = DependencyProperty.Register(nameof(HighlightChecked), typeof(bool),
                typeof(LabeledToggleButton));

        public bool HighlightChecked {
            get => GetValue(HighlightCheckedProperty) as bool? == true;
            set => SetValue(HighlightCheckedProperty, value);
        }

        public static readonly DependencyProperty AnimationSpeedProperty = DependencyProperty.Register(nameof(AnimationSpeed), typeof(double),
                typeof(LabeledToggleButton), new PropertyMetadata(150d));

        public double AnimationSpeed {
            get => GetValue(AnimationSpeedProperty) as double? ?? 0d;
            set => SetValue(AnimationSpeedProperty, value);
        }

        private DateTime _previous;

        private void OnRendering(object sender, RenderingEventArgs e) {
            if (!_animateTo.HasValue) return;

            var delta = (DateTime.Now - _previous).TotalMilliseconds / AnimationSpeed;
            _previous = DateTime.Now;
            SetPosition(_animateTo.Value < _position ? _position - delta : _position + delta);
        }

        private void AnimateTo(double value) {
            _previous = DateTime.Now;
            _animateTo = value;
        }

        private double? _animateTo;
        private double _position;

        private FrameworkElement _labelOn, _labelOff, _helper;
        private Thumb _thumb;

        public override void OnApplyTemplate() {
            if (_thumb != null) {
                _thumb.DragDelta -= Thumb_DragDelta;
                _thumb.DragCompleted -= Thumb_DragCompleted;
            }

            base.OnApplyTemplate();

            _labelOn = GetTemplateChild("PART_OnLabel") as FrameworkElement;
            _labelOff = GetTemplateChild("PART_OffLabel") as FrameworkElement;
            _helper = GetTemplateChild("PART_Helper") as FrameworkElement;
            _thumb = GetTemplateChild("PART_Thumb") as Thumb;

            if (_thumb != null) {
                _thumb.DragDelta += Thumb_DragDelta;
                _thumb.DragCompleted += Thumb_DragCompleted;
            }

            if (!Equals(_position, 0d)) {
                SetPosition(_position);
            }
        }

        protected override void OnUnchecked(RoutedEventArgs e) {
            base.OnUnchecked(e);
            AnimateTo(0d);
        }

        protected override void OnChecked(RoutedEventArgs e) {
            base.OnChecked(e);

            if (IsLoaded) {
                AnimateTo(1d);
            } else {
                SetPosition(1d);
            }
        }

        private void SetPosition(double pos) {
            if (pos < 0d) pos = 0d;
            if (pos > 1d) pos = 1d;

            if (_animateTo.HasValue && Math.Abs(_animateTo.Value - pos) < 0.0001) {
                _animateTo = null;
            }

            _position = pos;
            if (_thumb != null) {
                RelativeTranslateTransform.SetX(_thumb, pos);
                RelativeTranslateTransform.SetX(_labelOff, pos);
                RelativeTranslateTransform.SetX(_labelOn, pos - 1d);
            }
        }

        private double _lastChange;

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e) {
            _animateTo = null;
            _lastChange = e.HorizontalChange / _helper.ActualWidth;
            SetPosition(_position + _lastChange);
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e) {
            var next = _position + _lastChange * (5 + _helper.ActualWidth / 50d);
            if (IsChecked == next > 0.5d) {
                AnimateTo(IsChecked == true ? 1d : 0d);
            } else {
                IsChecked = !IsChecked;
            }
        }

        public static readonly DependencyProperty LabelUncheckedProperty = DependencyProperty.Register(nameof(LabelUnchecked), typeof(string),
                typeof(LabeledToggleButton));

        public string LabelUnchecked {
            get => (string)GetValue(LabelUncheckedProperty);
            set => SetValue(LabelUncheckedProperty, value);
        }

        public static readonly DependencyProperty LabelCheckedProperty = DependencyProperty.Register(nameof(LabelChecked), typeof(string),
                typeof(LabeledToggleButton));

        public string LabelChecked {
            get => (string)GetValue(LabelCheckedProperty);
            set => SetValue(LabelCheckedProperty, value);
        }
    }
}