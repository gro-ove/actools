using System.Windows;
using System.Windows.Controls;

namespace AcManager.Pages.Workshop {
    public class FixedCell : Panel {
        private Size _measuredActualSize;
        private Size _measuredSizePrev;
        private Size _arrangedActualSize;

        protected override Size MeasureOverride(Size constraint) {
            if (_measuredActualSize == constraint) {
                return _measuredSizePrev;
            }
            _measuredActualSize = constraint;

            var width = 0d;
            var height = 0d;

            var children = InternalChildren;
            for (int i = 0, count = children.Count; i < count; ++i) {
                var child = children[i];
                child.Measure(constraint);

                var size = child.DesiredSize;
                if (size.Width > width) width = size.Width;
                if (size.Height > height) height = size.Height;
            }

            return _measuredSizePrev = new Size(width, height);
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            var cachedRun = _arrangedActualSize == _measuredActualSize;
            _arrangedActualSize = _measuredActualSize;
            var rect = new Rect(arrangeBounds);
            var children = InternalChildren;
            for (int i = 0, count = children.Count; i < count; ++i) {
                if (cachedRun && GetFixedSize(children[i])) continue;
                children[i].Arrange(rect);
            }
            return arrangeBounds;
        }

        public static bool GetFixedSize(DependencyObject obj) {
            return (bool)obj.GetValue(FixedSizeProperty);
        }

        public static void SetFixedSize(DependencyObject obj, bool value) {
            obj.SetValue(FixedSizeProperty, value);
        }

        public static readonly DependencyProperty FixedSizeProperty = DependencyProperty.RegisterAttached("FixedSize", typeof(bool),
                typeof(FixedCell), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    }

    public class AnimatableScrollViewer : ScrollViewer {
        public AnimatableScrollViewer() {
            DefaultStyleKey = typeof(ScrollViewer);
        }

        public static readonly DependencyProperty CustomHorizontalOffsetProperty = DependencyProperty.Register(
                "CustomHorizontalOffset", typeof(double), typeof(AnimatableScrollViewer),
                new PropertyMetadata(OnChanged));

        public double CustomHorizontalOffset
        {
            get => (double)GetValue(HorizontalOffsetProperty);
            set => ScrollToHorizontalOffset(value);
        }
        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatableScrollViewer)d).CustomHorizontalOffset = (double)e.NewValue;
        }
    }
}