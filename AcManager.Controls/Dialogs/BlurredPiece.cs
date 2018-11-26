using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.Dialogs {
    public class BlurredPiece : Control {
        public static StoredValue<bool> IsActive { get; } = Stored.Get("BlurredPiece.IsActive", false);

        public BlurredPiece() {
            DefaultStyleKey = typeof(BlurredPiece);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _loaded = true;
            if (_visual is FrameworkElement el) {
                el.SizeChanged += Visual_SizeChanged;
                UpdateOffset();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            _loaded = false;
            if (_visual is FrameworkElement el) {
                el.SizeChanged -= Visual_SizeChanged;
            }
        }

        public static readonly DependencyProperty VisualProperty = DependencyProperty.Register(nameof(Visual), typeof(Visual),
                typeof(BlurredPiece), new PropertyMetadata(OnVisualChanged));

        public Visual Visual {
            get => (Visual)GetValue(VisualProperty);
            set => SetValue(VisualProperty, value);
        }

        private static void OnVisualChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BlurredPiece)o).OnVisualChanged((Visual)e.OldValue, (Visual)e.NewValue);
        }

        private void OnVisualChanged(Visual oldValue, Visual newValue) {
            if (oldValue is FrameworkElement e) {
                e.SizeChanged -= Visual_SizeChanged;
            }

            _visual = newValue;
            UpdateOffset();

            e = newValue as FrameworkElement;
            if (e != null) {
                e.SizeChanged += Visual_SizeChanged;
            }
        }

        private void Visual_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateOffset();
        }

        public static readonly DependencyProperty ViewboxProperty = DependencyProperty.Register(nameof(Viewbox), typeof(Rect),
                typeof(BlurredPiece));

        public Rect Viewbox {
            get => (Rect)GetValue(ViewboxProperty);
            set => SetValue(ViewboxProperty, value);
        }

        public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(nameof(BlurRadius), typeof(double),
                typeof(BlurredPiece), new PropertyMetadata(OnBlurRadiusChanged));

        public double BlurRadius {
            get => (double)GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        private static void OnBlurRadiusChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BlurredPiece)o).OnBlurRadiusChanged((double)e.NewValue);
        }

        private Visual _visual;
        private double _blurRadius;

        private void OnBlurRadiusChanged(double newValue) {
            _blurRadius = newValue;
            SetValue(InnerMarginPropertyKey, new Thickness(-_blurRadius));
        }

        public static readonly DependencyPropertyKey InnerMarginPropertyKey = DependencyProperty.RegisterReadOnly(nameof(InnerMargin), typeof(Thickness),
                typeof(BlurredPiece), new PropertyMetadata(default(Thickness)));

        public static readonly DependencyProperty InnerMarginProperty = InnerMarginPropertyKey.DependencyProperty;

        public Thickness InnerMargin => (Thickness)GetValue(InnerMarginProperty);

        protected override Size ArrangeOverride(Size arrangeBounds) {
            UpdateOffset();
            return base.ArrangeOverride(arrangeBounds);
        }

        private readonly Busy _updateOffset = new Busy();

        private void UpdateOffset() {
            _updateOffset.Yield(() => {
                try {
                    if (_loaded && _visual != null) {
                        var offset = _visual.PointFromScreen(PointToScreen(default));
                        Viewbox = new Rect(offset.X - _blurRadius, offset.Y - _blurRadius,
                                ActualWidth + _blurRadius * 2, ActualHeight + _blurRadius * 2);
                    }
                } catch (InvalidOperationException) { }
            });
        }
    }
}