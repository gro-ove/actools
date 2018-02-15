using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FirstFloor.ModernUI.Windows {
    public class DragAdorner : Adorner {
        private readonly Rectangle _child;
        private double _offsetLeft;
        private double _offsetTop;

        public DragAdorner(UIElement adornedElement, Size size, Brush brush) : base(adornedElement) {
            _child = new Rectangle {
                Fill = brush,
                Width = size.Width,
                Height = size.Height,
                IsHitTestVisible = false
            };
        }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform) {
            var result = new GeneralTransformGroup();
            var desired = base.GetDesiredTransform(transform);
            if (desired != null) {
                result.Children.Add(desired);
            }

            result.Children.Add(new TranslateTransform(_offsetLeft, _offsetTop));
            return result;
        }

        public double OffsetLeft {
            get => _offsetLeft;
            set {
                _offsetLeft = value;
                UpdateLocation();
            }
        }

        public void SetOffsets(double left, double top) {
            _offsetLeft = left;
            _offsetTop = top;
            UpdateLocation();
        }

        public double OffsetTop {
            get => _offsetTop;
            set {
                _offsetTop = value;
                UpdateLocation();
            }
        }

        protected override Size MeasureOverride(Size constraint) {
            _child.Measure(constraint);
            return _child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize) {
            _child.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override Visual GetVisualChild(int index) {
            return _child;
        }

        protected override int VisualChildrenCount => 1;

        private void UpdateLocation() {
            (Parent as AdornerLayer)?.Update(AdornedElement);
        }
    }
}