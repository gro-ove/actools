using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

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

            MouseEventHandler handler;
            var grid = element as DataGrid;
            if (grid != null) {
                handler = DataGrid_MouseMove;
                grid.BeginningEdit += DataGrid_BeginningEdit;
                grid.CellEditEnding += DataGrid_CellEditEnding;
            } else if (element is ListBox) {
                handler = ListBox_MouseMove;
            } else {
                handler = Element_MouseMove;
            }

            if (e.Property == DataProperty && e.NewValue != null ||
                    e.Property == EnabledProperty && e.NewValue as bool? != false) {
                element.PreviewMouseDown += Element_MouseDown;
                element.PreviewMouseMove += handler;
                element.AllowDrop = true;
            } else {
                element.PreviewMouseDown -= Element_MouseDown;
                element.PreviewMouseMove -= handler;
                element.AllowDrop = false;
            }
        }

        private static void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e) {
            // throw new System.NotImplementedException();
        }

        private static void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) {
            // throw new System.NotImplementedException();
        }

        private static bool _dragging;
        private static object _previous;
        private static Point _startingPoint;

        private static void Element_MouseDown(object sender, MouseButtonEventArgs e) {
            _previous = e.Handled ? null : sender;
            _startingPoint = VisualExtension.GetMousePosition();
        }

        [CanBeNull]
        private static ItemsControl PrepareItem(FrameworkElement element) {
            {
                var item = element as ListBoxItem;
                var parent = item?.GetParent<ListBox>();
                if (parent != null) {
                    parent.SelectedItem = item;
                    return parent;
                }
            }

            {
                var row = element as DataGridRow;
                var parent = row?.GetParent<DataGrid>();
                if (parent != null) {
                    parent.SelectedItem = row.Item;
                    return parent;
                }
            }

            return null;
        }

        private static bool MoveDraggable([CanBeNull] FrameworkElement element, [CanBeNull] IDraggable draggable) {
            if (element == null || draggable == null) return false;

            try {
                _dragging = true;

                var data = new DataObject();
                data.SetData(draggable.DraggableFormat, draggable);

                var source = PrepareItem(element);
                if (source != null) {
                    data.SetData(SourceFormat, source);
                }

                using (new DragPreview(element)) {
                    DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
                }
            } finally {
                _dragging = false;
            }

            return true;
        }

        private static bool MoveBasic(FrameworkElement element) {
            return element != null && MoveDraggable(element, (GetData(element) ?? element.DataContext) as IDraggable);
        }

        private static bool MoveFromListBox(IInputElement element, MouseEventArgs e) {
            return MoveBasic((element as ListBox)?.GetFromPoint<ListBoxItem>(e.GetPosition(element)));
        }

        private static bool MoveFromDataGrid(FrameworkElement element, MouseEventArgs e) {
            var row = (element as DataGrid)?.GetFromPoint<DataGridRow>(e.GetPosition(element));
            return row != null && MoveDraggable(row, row.Item as IDraggable);
        }

        private static readonly Type[] IgnoredControls = {
            typeof(TextBoxBase),
            typeof(PasswordBox),
            typeof(Thumb)
        };

        private static bool IgnoreSpecialControls(object sender, MouseEventArgs e) {
            var reference = sender as UIElement;
            var element = reference?.InputHitTest(e.GetPosition(reference)) as DependencyObject;
            return element == null || new[] { element }.Union(element.GetParents())
                                                       .TakeWhile(x => !ReferenceEquals(x, sender))
                                                       .Any(parent => IgnoredControls.Contains(parent.GetType()));
        }

        private static bool IsDragging(object sender, MouseEventArgs e) {
            if (_dragging || _previous != sender || e.LeftButton != MouseButtonState.Pressed ||
                    VisualExtension.GetMousePosition().DistanceTo(_startingPoint) < 4d) return false;
            if (IgnoreSpecialControls(sender, e)) {
                _previous = null;
                return false;
            }

            return true;
        }

        private static void Element_MouseMove(object sender, MouseEventArgs e) {
            e.Handled = e.Handled || IsDragging(sender, e) && MoveBasic((FrameworkElement)sender);
        }

        private static void ListBox_MouseMove(object sender, MouseEventArgs e) {
            e.Handled = e.Handled || IsDragging(sender, e) && MoveFromListBox((FrameworkElement)sender, e);
        }

        private static void DataGrid_MouseMove(object sender, MouseEventArgs e) {
            e.Handled = e.Handled || IsDragging(sender, e) && MoveFromDataGrid((FrameworkElement)sender, e);
        }
    }
}