using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BetterComboBox : ComboBox {
        static BetterComboBox() {
            IsEditableProperty.OverrideMetadata(typeof(BetterComboBox), new FrameworkPropertyMetadata(true));
        }

        public BetterComboBox() {
            DefaultStyleKey = typeof(BetterComboBox);
            PreviewMouseWheel += OnMouseWheel;
            Loaded += OnLoaded;
        }

        public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(nameof(MaxLength), typeof(int),
                typeof(BetterComboBox));

        public int MaxLength {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(BetterComboBox));

        public string Placeholder {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            if (IsDropDownOpen) return;

            e.Handled = true;
            (Parent as UIElement)?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            });
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            FocusAdvancement.OnKeyDown(this, e);

            if (!e.Handled) {
                base.OnPreviewKeyDown(e);
            }
        }

        public new static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object),
                typeof(BetterComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        [CanBeNull]
        public new object SelectedItem {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private static void OnSelectedItemChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterComboBox)o).OnSelectedItemChanged(e.NewValue);
        }

        private void OnSelectedItemChanged(object newValue) {
            SetValue(Selector.SelectedItemProperty, newValue ?? NullValue);
        }

        public static bool IgnoreUnloadedChanges = false;
        private bool _updateSelectedItemLater;

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            base.OnSelectionChanged(e);
            if (!IgnoreUnloadedChanges || IsLoaded) {
                SelectedItem = ReferenceEquals(base.SelectedItem, NullValue) ? null : base.SelectedItem;
            } else {
                _updateSelectedItemLater = true;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_updateSelectedItemLater) {
                _updateSelectedItemLater = false;
                SelectedItem = ReferenceEquals(base.SelectedItem, NullValue) ? null : base.SelectedItem;
            }
        }

        private class NullValueClass {
            public override string ToString() {
                return @"null";
            }
        }

        public static readonly object NullValue = new NullValueClass();

        private class NullPrefixedCollection : BetterObservableCollection<object> {
            private readonly IEnumerable _collection;

            public NullPrefixedCollection(IEnumerable collection)
                    // ReSharper disable once PossibleMultipleEnumeration
                    : base(new[] { NullValue }.Concat(collection.OfType<object>())) {
                if (collection is INotifyCollectionChanged o) {
                    // ReSharper disable once PossibleMultipleEnumeration
                    _collection = collection;
                    WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(o, nameof(o.CollectionChanged),
                            OnCollectionChanged);
                }
            }

            private void Rebuild() {
                ReplaceEverythingBy(new[] { NullValue }.Concat(_collection.OfType<object>()));
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
                switch (e.Action) {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems?.Count == 1 && e.NewStartingIndex + 1 > 0) {
                            if (e.NewStartingIndex + 1 >= Count) {
                                Add(e.NewItems[0]);
                            } else {
                                Insert(e.NewStartingIndex + 1, e.NewItems[0]);
                            }
                        } else {
                            Rebuild();
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null) {
                            foreach (var oldItem in e.OldItems) {
                                Remove(oldItem);
                            }
                        } else {
                            Rebuild();
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        if (e.OldItems?.Count == 1 && e.NewItems?.Count == 1) {
                            var i = IndexOf(e.OldItems[0]);
                            if (i != -1) {
                                this[i] = e.NewItems[0];
                            } else {
                                Rebuild();
                            }
                        } else {
                            Rebuild();
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        Move(e.OldStartingIndex + 1, e.NewStartingIndex + 1);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        Rebuild();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void UpdateItemsSource() {
            var items = ItemsSource;
            var nullable = Nullable;
            SetValue(ItemsControl.ItemsSourceProperty,
                    nullable ? items == null ? (IEnumerable)new object[] { null } : new NullPrefixedCollection(items) : ItemsSource);
            if (nullable && base.SelectedItem == null) {
                base.SelectedItem = NullValue;
            }
        }

        public new static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable),
                typeof(BetterComboBox), new PropertyMetadata(OnItemsSourceChanged));

        public new IEnumerable ItemsSource {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterComboBox)o).UpdateItemsSource();
        }

        public static readonly DependencyProperty NullableProperty = DependencyProperty.Register(nameof(Nullable), typeof(bool),
                typeof(BetterComboBox), new PropertyMetadata(OnNullableChanged));

        public bool Nullable {
            get => GetValue(NullableProperty) as bool? == true;
            set => SetValue(NullableProperty, value);
        }

        private static void OnNullableChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterComboBox)o).UpdateItemsSource();
        }
    }
}