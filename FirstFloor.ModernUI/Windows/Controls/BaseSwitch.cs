using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseSwitch : Panel {
        protected abstract bool TestChild(DependencyObject child);

        protected object GetChild() {
            return InternalChildren.OfType<DependencyObject>().FirstOrDefault(TestChild);
        }

        protected override Size MeasureOverride(Size constraint) {
            var e = GetChild() as UIElement;
            if (e == null) return Size.Empty;

            e.Measure(constraint);
            return e.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            var e = GetChild() as UIElement;
            if (e == null) return Size.Empty;

            e.Arrange(new Rect(arrangeBounds));
            return e.RenderSize;
        }

        protected override int VisualChildrenCount => GetChild() is UIElement ? 1 : 0;

        protected override Visual GetVisualChild(int index) {
            return GetChild() as UIElement;
        }
        
        protected static void OnWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as UIElement;
            if (element != null) {
                (VisualTreeHelper.GetParent(element) as BaseSwitch)?.InvalidateMeasure();
            }
        }
    }

    public class Switch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(Switch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public object Value {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static object GetWhen(DependencyObject obj) {
            return (object)obj.GetValue(WhenProperty);
        }

        public static void SetWhen(DependencyObject obj, object value) {
            obj.SetValue(WhenProperty, value);
        }

        public static readonly DependencyProperty WhenProperty = DependencyProperty.RegisterAttached("When", typeof(object),
                typeof(Switch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsParentMeasure, OnWhenChanged));

        protected override bool TestChild(DependencyObject child) {
            return Equals(Value, GetWhen(child));
        }
    }

    public class BooleanSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(bool),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool Value {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static bool GetWhen(DependencyObject obj) {
            return (bool)obj.GetValue(WhenProperty);
        }

        public static void SetWhen(DependencyObject obj, bool value) {
            obj.SetValue(WhenProperty, value);
        }

        public static readonly DependencyProperty WhenProperty = DependencyProperty.RegisterAttached("When", typeof(bool),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure, OnWhenChanged));

        protected override bool TestChild(DependencyObject child) {
            return Value == GetWhen(child);
        }
    }
}