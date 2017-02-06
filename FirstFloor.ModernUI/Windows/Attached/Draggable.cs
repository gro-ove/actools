using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class DraggableMovedEventArgs : EventArgs {
        public DraggableMovedEventArgs(string format, object draggable) {
            Format = format;
            Draggable = draggable;
        }

        public string Format { get; }

        public object Draggable { get; }
    }

    public static class Draggable {
        public const string SourceFormat = "Data-Source";

        private static readonly DraggableEvents Events = new DraggableEvents();

        private class DraggableEvents {
            public event EventHandler<DraggableMovedEventArgs> DragStartedInner;
            public event EventHandler<DraggableMovedEventArgs> DragEndedInner;

            internal void RaiseDragStarted(string format, object obj) {
                DragStartedInner?.Invoke(this, new DraggableMovedEventArgs(format, obj));
            }

            internal void RaiseDragEnded(string format, object obj) {
                DragEndedInner?.Invoke(this, new DraggableMovedEventArgs(format, obj));
            }
        }

        /// <summary>
        /// Weak event.
        /// </summary>
        public static event EventHandler<DraggableMovedEventArgs> DragStarted {
            add { WeakEventManager<DraggableEvents, DraggableMovedEventArgs>.AddHandler(Events, nameof(Events.DragStartedInner), value); }
            remove { WeakEventManager<DraggableEvents, DraggableMovedEventArgs>.RemoveHandler(Events, nameof(Events.DragStartedInner), value); }
        }

        /// <summary>
        /// Weak event.
        /// </summary>
        public static event EventHandler<DraggableMovedEventArgs> DragEnded {
            add { WeakEventManager<DraggableEvents, DraggableMovedEventArgs>.AddHandler(Events, nameof(Events.DragEndedInner), value); }
            remove { WeakEventManager<DraggableEvents, DraggableMovedEventArgs>.RemoveHandler(Events, nameof(Events.DragEndedInner), value); }
        }

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

        public static bool GetKeepSelection(DependencyObject obj) {
            return (bool)obj.GetValue(KeepSelectionProperty);
        }

        public static void SetKeepSelection(DependencyObject obj, bool value) {
            obj.SetValue(KeepSelectionProperty, value);
        }

        public static readonly DependencyProperty KeepSelectionProperty = DependencyProperty.RegisterAttached("KeepSelection", typeof(bool),
                typeof(Draggable), new PropertyMetadata(false));

        public static bool GetIsDestinationHighlighted(DependencyObject obj) {
            return (bool)obj.GetValue(IsDestinationHighlightedProperty);
        }

        public static void SetIsDestinationHighlighted(DependencyObject obj, bool value) {
            obj.SetValue(IsDestinationHighlightedProperty, value);
        }

        public static readonly DependencyProperty IsDestinationHighlightedProperty = DependencyProperty.RegisterAttached("IsDestinationHighlighted", typeof(bool),
                typeof(Draggable), new FrameworkPropertyMetadata(false));

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = (FrameworkElement)d;

            if (e.Property == DataProperty && (GetEnabled(element) || e.OldValue != null)) return;
            if (e.Property == EnabledProperty && (GetData(element) != null || e.OldValue as bool? != false)) return;

            MouseEventHandler handler;
            var grid = element as DataGrid;
            if (grid != null) {
                handler = DataGrid_MouseMove;
            } else if (element is ListBox) {
                handler = ListBox_MouseMove;
            } else if (element is ItemsControl) {
                handler = ItemsControl_MouseMove;
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

        private static bool _dragging;
        private static object _previous;
        private static Point _startingPoint;

        private static void Element_MouseDown(object sender, MouseButtonEventArgs e) {
            if (!e.Handled) {
                var element = sender as FrameworkElement;
                if (element != null && !IgnoreSpecialControls(sender, e)) {
                    _previous = element;
                    _startingPoint = VisualExtension.GetMousePosition();
                    return;
                }
            }

            _previous = null;
        }

        [CanBeNull]
        private static ItemsControl PrepareItem(FrameworkElement element) {
            {
                var item = element as ListBoxItem;
                var parent = item?.GetParent<ListBox>();
                if (parent != null) {
                    if (!GetKeepSelection(parent)) {
                        parent.SelectedItem = item;
                    }
                    return parent;
                }
            }

            {
                var row = element as DataGridRow;
                var parent = row?.GetParent<DataGrid>();
                if (parent != null) {
                    if (!GetKeepSelection(parent)) {
                        parent.SelectedItem = row.Item;
                    }
                    return parent;
                }
            }

            return null;
        }

        private static void MarkDestinations(string format) {
            var app = Application.Current;
            if (app == null) return;
            foreach (var destination in app.Windows.OfType<Window>()
                    .SelectMany(VisualTreeHelperEx.FindVisualChildren<FrameworkElement>)
                    .Where(x => string.Equals(GetDestination(x), format, StringComparison.Ordinal))) {
                SetIsDestinationHighlighted(destination, true);
            }
        }

        private static void UnmarkDestinations() {
            var app = Application.Current;
            if (app == null) return;
            foreach (var destination in app.Windows.OfType<Window>()
                    .SelectMany(VisualTreeHelperEx.FindVisualChildren<FrameworkElement>)
                    .Where(GetIsDestinationHighlighted)) {
                SetIsDestinationHighlighted(destination, false);
            }
        }

        private static bool MoveDraggable([CanBeNull] FrameworkElement element, [CanBeNull] IDraggable draggable) {
            if (element == null || draggable == null) return false;

            try {
                _dragging = true;

                var data = new DataObject();
                data.SetData(draggable.DraggableFormat, draggable);
                MarkDestinations(draggable.DraggableFormat);
                Events.RaiseDragStarted(draggable.DraggableFormat, draggable);

                var source = PrepareItem(element);
                if (source != null) {
                    data.SetData(SourceFormat, source);
                }

                using (new DragPreview(element)) {
                    DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
                }
            } finally {
                _dragging = false;
                UnmarkDestinations();
                Events.RaiseDragEnded(draggable.DraggableFormat, draggable);
            }

            return true;
        }

        private static bool MoveBasic(FrameworkElement element) {
            return element != null && MoveDraggable(element, (GetData(element) ?? element.DataContext) as IDraggable);
        }

        private static bool MoveFromListBox(IInputElement element, MouseEventArgs e) {
            return MoveBasic((element as ListBox)?.GetFromPoint<ListBoxItem>(e.GetPosition(element)));
        }

        private static bool MoveFromItemsControl(IInputElement element, MouseEventArgs e) {
            var item = (element as ItemsControl)?.GetFromPoint<FrameworkElement>(e.GetPosition(element))?
                                                 .GetParents()
                                                 .OfType<FrameworkElement>()
                                                 .FirstOrDefault(x => ReferenceEquals(x.TemplatedParent, element));
            return MoveBasic(item);
        }

        private static bool MoveFromDataGrid(FrameworkElement element, MouseEventArgs e) {
            var row = (element as DataGrid)?.GetFromPoint<DataGridRow>(e.GetPosition(element));
            return row != null && MoveDraggable(row, row.Item as IDraggable);
        }

        private static bool IsIgnored(DependencyObject obj) {
            var textBox = obj as TextBoxBase;
            if (textBox != null) {
                return textBox.IsEnabled;
            }

            var thumb = obj as Thumb;
            if (thumb != null) {
                return thumb.IsEnabled;
            }

            var passwordBox = obj as PasswordBox;
            if (passwordBox != null) {
                return passwordBox.IsEnabled;
            }

            return false;
        }

        private static bool IgnoreSpecialControls(object sender, MouseEventArgs e) {
            var reference = sender as UIElement;
            var element = reference?.InputHitTest(e.GetPosition(reference)) as DependencyObject;
            if (element == null || IsIgnored(element)) return true;
            return element.GetParents().TakeWhile(parent => !ReferenceEquals(parent, sender)).Any(IsIgnored);
        }

        private static bool IsDragging(object sender, MouseEventArgs e) {
            if (_dragging || _previous != sender || e.LeftButton != MouseButtonState.Pressed ||
                    VisualExtension.GetMousePosition().DistanceTo(_startingPoint) < 3d) return false;
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

        private static void ItemsControl_MouseMove(object sender, MouseEventArgs e) {
            e.Handled = e.Handled || IsDragging(sender, e) && MoveFromItemsControl((FrameworkElement)sender, e);
        }

        private static void DataGrid_MouseMove(object sender, MouseEventArgs e) {
            e.Handled = e.Handled || IsDragging(sender, e) && MoveFromDataGrid((FrameworkElement)sender, e);
        }

        public static string GetDestination(DependencyObject obj) {
            return (string)obj.GetValue(DestinationProperty);
        }

        public static void SetDestination(DependencyObject obj, string value) {
            obj.SetValue(DestinationProperty, value);
        }

        public static readonly DependencyProperty DestinationProperty = DependencyProperty.RegisterAttached("Destination", typeof(string),
                typeof(Draggable), new UIPropertyMetadata(OnDestinationChanged));

        private static void OnDestinationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as ItemsControl;
            if (element == null || !(e.NewValue is string)) return;

            var newValue = (string)e.NewValue;
            if (newValue != null) {
                element.Drop += Destination_Drop;
                element.AllowDrop = true;
            } else {
                element.Drop -= Destination_Drop;
                element.AllowDrop = false;
            }
        }

        public static IDraggableDestinationConverter GetDestinationConverter(DependencyObject obj) {
            return (IDraggableDestinationConverter)obj.GetValue(DestinationConverterProperty);
        }

        public static void SetDestinationConverter(DependencyObject obj, IDraggableDestinationConverter value) {
            obj.SetValue(DestinationConverterProperty, value);
        }

        public static readonly DependencyProperty DestinationConverterProperty = DependencyProperty.RegisterAttached("DestinationConverter", typeof(IDraggableDestinationConverter),
                typeof(Draggable), new UIPropertyMetadata(OnDestinationConverterChanged));

        private static void OnDestinationConverterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as ItemsControl;
            if (element == null || !(e.NewValue is IDraggableDestinationConverter)) return;
            
            var newValue = (IDraggableDestinationConverter)e.NewValue;
            if (newValue != null) {
                element.Drop += DestinationConverter_Drop;
                element.AllowDrop = true;
            } else {
                element.Drop -= DestinationConverter_Drop;
                element.AllowDrop = false;
            }
        }

        private static void Destination_Drop(object sender, DragEventArgs e) {
            var destination = (ItemsControl)sender;
            var format = GetDestination(destination);
            var item = e.Data.GetData(format) as IDraggable;
            var source = e.Data.GetData(SourceFormat) as ItemsControl;
            InnerDrop(source, destination, item, e);
        }

        private static void DestinationConverter_Drop(object sender, DragEventArgs e) {
            var destination = (ItemsControl)sender;
            var converter = GetDestinationConverter(destination);
            var item = converter?.Convert(e.Data);
            var source = e.Data.GetData(SourceFormat) as ItemsControl;
            InnerDrop(source, destination, item, e);
        }

        [CanBeNull]
        private static IEnumerable GetActualList([CanBeNull] ItemsControl itemsControl) {
            var list = itemsControl?.ItemsSource;
            if (list is CompositeCollection) {
                list = ((CompositeCollection)list).OfType<CollectionContainer>()
                                                                       .Select(x => x.Collection)
                                                                       .OfType<ListCollectionView>()
                                                                       .Select(x => x.SourceCollection)
                                                                       .FirstOrDefault();
            }

            return list;
        }

        private static void InnerDrop([CanBeNull] ItemsControl source, [NotNull] ItemsControl destination, [CanBeNull] object item, DragEventArgs e) {
            if (item == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            var newIndex = destination.GetMouseItemIndex();
            var type = item.GetType();

            try {
                var destinationList = GetActualList(destination) as IList;
                var ss = GetActualList(source);
                var sourceList = ss as IList;

                if (destinationList == null) {
                    Logging.Warning("Can’t find target: " + destination.ItemsSource);
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                if (newIndex >= destinationList.Count) {
                    newIndex = -1;
                }

                if (newIndex == -1) {
                    var method = (from x in destinationList.GetType().GetMethods()
                                  where x.Name == "Add"
                                  let p = x.GetParameters()
                                  where p.Length == 1 && p[0].ParameterType == type
                                  select x).FirstOrDefault();
                    if (method == null) {
                        e.Effects = DragDropEffects.None;
                        return;
                    }

                    sourceList?.Remove(item);
                    method.Invoke(destinationList, new[] { item });
                } else {
                    var method = (from x in destinationList.GetType().GetMethods()
                                  where x.Name == "Insert"
                                  let p = x.GetParameters()
                                  where p.Length == 2 && p[1].ParameterType == type
                                  select x).FirstOrDefault();
                    if (method == null) {
                        e.Effects = DragDropEffects.None;
                        return;
                    }

                    sourceList?.Remove(item);
                    method.Invoke(destinationList, new[] { newIndex, item });
                }

                e.Effects = DragDropEffects.Move;
            } catch (Exception ex) {
                Logging.Debug(ex);
                e.Effects = DragDropEffects.None;
            }
        }
    }

    public interface IDraggableDestinationConverter {
        [CanBeNull]
        object Convert([NotNull] IDataObject data);
    }
}