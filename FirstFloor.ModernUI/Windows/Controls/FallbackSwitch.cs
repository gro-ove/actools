using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class FallbackSwitch : Panel {
        private UIElement _activeChild;

        private void UpdateActiveChild() {
            var activeChild = InternalChildren.OfType<UIElement>().FirstOrDefault(TestChild);
            if (ReferenceEquals(_activeChild, activeChild)) return;
            if (_activeChild != null) {
                _activeChild.Visibility = Visibility.Collapsed;
            }
            _activeChild = activeChild;
            if (activeChild != null) {
                activeChild.Visibility = Visibility.Visible;
            }
        }

        protected override Size MeasureOverride(Size constraint) {
            UpdateActiveChild();

            var child = _activeChild;
            if (child == null) {
                return new Size();
            }

            child.Measure(constraint);
            return child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            UpdateActiveChild();
            _activeChild?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        public static object GetValue(DependencyObject obj) {
            return obj.GetValue(ValueProperty);
        }

        public static void SetValue(DependencyObject obj, object value) {
            obj.SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(object),
                typeof(FallbackSwitch), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        public static object GetWhen(DependencyObject obj) {
            return obj.GetValue(WhenProperty);
        }

        public static void SetWhen(DependencyObject obj, object value) {
            obj.SetValue(WhenProperty, value);
        }

        public static readonly DependencyProperty WhenProperty = DependencyProperty.RegisterAttached("When", typeof(object),
                typeof(FallbackSwitch), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        private static readonly object UnsetValue = new object();

        public static object GetWhenNot(DependencyObject obj) {
            return obj.GetValue(WhenNotProperty);
        }

        public static void SetWhenNot(DependencyObject obj, object value) {
            obj.SetValue(WhenNotProperty, value);
        }

        public static readonly DependencyProperty WhenNotProperty = DependencyProperty.RegisterAttached("WhenNot", typeof(object),
                typeof(FallbackSwitch), new FrameworkPropertyMetadata(UnsetValue, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        protected bool TestChild(UIElement child) {
            var value = GetValue(child);
            var whenNot = GetWhenNot(child);
            if (!ReferenceEquals(whenNot, UnsetValue)) {
                return !value.XamlEquals(whenNot);
            }
            return value.XamlEquals(GetWhen(child));
        }
    }
}