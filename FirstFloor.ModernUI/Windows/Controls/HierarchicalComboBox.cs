using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

// The only flaw of this solution is that HierarchicalComboBox wraps all provided items
// in HierarchicalItem not on-demand, but immediately after ItemsSource was assigned (or
// changed). Well, apart from the fact that HierarchicalComboBox do not support a lot of
// regular ComboBox stuff such as DisplayMemberPath property or OnSelectionChanged event.
// But, I think, they could be implemented when (and if) they will be needed.

namespace FirstFloor.ModernUI.Windows.Controls {
    public class HierarchicalGroup : BetterObservableCollection<object> {
        public HierarchicalGroup() {}

        public HierarchicalGroup(string displayName, List<object> list) : base(list) {
            _displayName = displayName;
        }

        public HierarchicalGroup(string displayName, [NotNull] IEnumerable<object> collection) : base(collection) {
            _displayName = displayName;
        }

        public HierarchicalGroup(string displayName) {
            _displayName = displayName;
        }

        private string _displayName;

        public string DisplayName {
            get { return _displayName; }
            set {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }
    }

    internal class HierarchicalItem : MenuItem {
        [NotNull]
        public MenuItemsView ParentView { get; }

        [CanBeNull]
        public object OriginalValue { get; }

        static HierarchicalItem() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HierarchicalItem), new FrameworkPropertyMetadata(typeof(HierarchicalItem)));
        }

        public HierarchicalItem(MenuItemsView parentView, object originalValue) {
            ParentView = parentView;
            OriginalValue = originalValue;
        }
    }

    internal class MenuItemsView : BetterObservableCollection<UIElement>, IDisposable, ICommand {
        [NotNull]
        private readonly HierarchicalComboBox _parent;

        [CanBeNull]
        private IList _source;

        [CanBeNull]
        private INotifyCollectionChanged _notify;

        public MenuItemsView([NotNull] HierarchicalComboBox parent) {
            _parent = parent;
        }

        public MenuItemsView([NotNull] HierarchicalComboBox parent, [CanBeNull] IList source) {
            _parent = parent;
            _source = source;
            _notify = _source as INotifyCollectionChanged;
            if (_notify != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(_notify, nameof(_notify.CollectionChanged),
                        OnItemsSourceCollectionChanged);
            }

            Rebuild();
        }

        public void SetSource([CanBeNull] IList source) {
            Dispose();

            var changed = _source == null != (source == null);
            _source = source;
            _notify = _source as INotifyCollectionChanged;
            if (_notify != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(_notify, nameof(_notify.CollectionChanged),
                        OnItemsSourceCollectionChanged);
            }

            Rebuild();

            if (changed) {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [ValueConversion(typeof(int), typeof(Visibility))]
        private class InnerCountToVisibilityConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value.AsInt() > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        private UIElement Wrap([CanBeNull] object value) {
            var direct = value as UIElement;
            if (direct != null) {
                return direct;
            }

            var result = new HierarchicalItem(this, value);

            if (value == null) return result;

            result.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding {
                Source = value,
                Path = new PropertyPath(@"DisplayName"),
                Mode = BindingMode.OneWay
            });

            var group = value as HierarchicalGroup;
            if (group == null) {
                result.Command = this;
                result.CommandParameter = value;
            } else {
                result.ItemsSource = new MenuItemsView(_parent, group);
                result.SetBinding(UIElement.VisibilityProperty, new Binding {
                    Source = group,
                    Path = new PropertyPath(nameof(HierarchicalGroup.Count)),
                    Mode = BindingMode.OneWay,
                    Converter = new InnerCountToVisibilityConverter()
                });
            }

            return result;
        }

        private void Rebuild() {
            ReplaceEverythingBy(_source?.OfType<object>().Select(Wrap) ?? new HierarchicalItem[0]);
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (_source == null || e.Action == NotifyCollectionChangedAction.Reset) {
                return;
            }

            foreach (var o in e.OldItems) {
                var removed = this.Skip(e.OldStartingIndex).OfType<HierarchicalItem>().FirstOrDefault(x => x.Tag == o);
                if (removed == null) continue;

                (removed.ItemsSource as MenuItemsView)?.Dispose();
                Remove(removed);
            }

            foreach (var n in e.NewItems) {
                var index = _source.IndexOf(n);
                if (index < 0 || index >= _source.Count - 1) {
                    Add(Wrap(n));
                } else {
                    Insert(index, Wrap(n));
                }
            }
        }

        public void Dispose() {
            if (_notify == null) return;
            WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.RemoveHandler(_notify, nameof(_notify.CollectionChanged),
                    OnItemsSourceCollectionChanged);
        }

        public bool CanExecute(object parameter) {
            return _source != null;
        }

        public void Execute(object parameter) {
            _parent.SelectedItem = parameter;
        }

        public event EventHandler CanExecuteChanged;
    }

    public class HierarchicalComboBox : Control {
        public HierarchicalComboBox() {
            DefaultStyleKey = typeof(HierarchicalComboBox);
        }

        public static readonly DependencyPropertyKey InnerItemsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(InnerItems), typeof(MenuItemsView),
                typeof(HierarchicalComboBox), new PropertyMetadata(null));

        public static readonly DependencyProperty InnerItemsProperty = InnerItemsPropertyKey.DependencyProperty;

        internal MenuItemsView InnerItems => (MenuItemsView)GetValue(InnerItemsProperty);

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IList),
                typeof(HierarchicalComboBox), new PropertyMetadata(OnItemsSourceChanged));

        public IList ItemsSource {
            get { return (IList)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        private static void OnItemsSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HierarchicalComboBox)o).OnItemsSourceChanged((IList)e.NewValue);
        }

        private IEnumerable<object> Flatten([NotNull] IList list) {
            foreach (var o in list) {
                var g = o as HierarchicalGroup;
                if (g != null) {
                    foreach (var c in Flatten(g)) {
                        yield return c;
                    }
                } else {
                    yield return o;
                }
            }
        }

        private void OnItemsSourceChanged([CanBeNull] IList newValue) {
            InnerItems?.Dispose();
            SetValue(InnerItemsPropertyKey, new MenuItemsView(this, newValue));

            if (FixedMode) {
                if (newValue == null) {
                    SelectedItem = null;
                } else if (!Flatten(newValue).Contains(SelectedItem)) {
                    SelectedItem = Flatten(newValue).FirstOrDefault();
                }
            }
        }

        /// <summary>
        /// Update SelectedItem when ItemsSource value is changed.
        /// </summary>
        public static readonly DependencyProperty FixedModeProperty = DependencyProperty.Register(nameof(FixedMode), typeof(bool),
                typeof(HierarchicalComboBox), new FrameworkPropertyMetadata(true));

        public bool FixedMode {
            get { return (bool)GetValue(FixedModeProperty); }
            set { SetValue(FixedModeProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object),
                typeof(HierarchicalComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object SelectedItem {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }
    }
}