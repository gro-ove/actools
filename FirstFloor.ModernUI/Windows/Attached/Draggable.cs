using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class Draggable {
        public const string SourceFormat = "Data-Source";

        public static bool GetEnabled(FrameworkElement obj) {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(FrameworkElement obj, bool value) {
            obj.SetValue(EnabledProperty, value);
        }   

        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(Draggable),
                new PropertyMetadata(false, OnEnabledChanged));

        public static object GetData(DependencyObject obj) {
            return obj.GetValue(DataProperty);
        }

        public static void SetData(DependencyObject obj, object value) {
            obj.SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty DataProperty = DependencyProperty.RegisterAttached("Data", typeof(object),
                typeof(Draggable), new PropertyMetadata(null, OnEnabledChanged));

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = (FrameworkElement)d;

            if (e.Property == DataProperty && (GetEnabled(element) || e.OldValue != null)) return;
            if (e.Property == EnabledProperty && (GetData(element) != null || e.OldValue as bool? != false)) return;

            if (e.Property == DataProperty && e.NewValue != null ||
                    e.Property == EnabledProperty && e.NewValue as bool? != false) {
                element.PreviewMouseDown += Element_PreviewMouseDown;
                element.PreviewMouseMove += Element_PreviewMouseMove;
            } else {
                element.PreviewMouseDown -= Element_PreviewMouseDown;
                element.PreviewMouseMove -= Element_PreviewMouseMove;
            }
        }

        private static bool _dragging;
        private static object _previous;
        private static Point _startingPoint;

        private static void Element_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _previous = sender;
            _startingPoint = VisualExtension.GetMousePosition();
        }

        private static void Element_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (_dragging || _previous != sender || e.LeftButton != MouseButtonState.Pressed ||
                    VisualExtension.GetMousePosition().DistanceTo(_startingPoint) < 4) return;

            Logging.Debug("source: " + e.OriginalSource);

            var element = (FrameworkElement)sender;
            var context = GetData(element) ?? element.DataContext;

            var draggable = context as IDraggable;
            if (draggable == null) return;

            try {
                _dragging = true;

                var data = new DataObject();
                data.SetData(draggable.DraggableFormat, context);

                var item = element as ListBoxItem;
                var parent = item?.GetParent<ListBox>();
                if (parent != null) {
                    parent.SelectedItem = item;
                    data.SetData(SourceFormat, parent);
                }
                
                using (new DragPreview(element)) {
                    DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
                }
            } finally {
                _dragging = false;
            }
        }
    }
}