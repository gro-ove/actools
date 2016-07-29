using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    internal class NewMarkAdorner : Adorner {
        private readonly FrameworkElement _contentPresenter;

        public NewMarkAdorner(UIElement adornedElement) : base(adornedElement) {
            IsHitTestVisible = true;

            _contentPresenter = new TextBlock {
                Style = FindResource(@"NewMarkStyle") as Style
            };

            SetBinding(VisibilityProperty, new Binding(@"IsVisible") {
                Source = adornedElement,
                Converter = new BooleanToVisibilityConverter()
            });
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) {
            return _contentPresenter;
        }

        protected override Size MeasureOverride(Size constraint) {
            _contentPresenter?.Measure(AdornedElement.RenderSize);
            return AdornedElement.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize) {
            _contentPresenter?.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}