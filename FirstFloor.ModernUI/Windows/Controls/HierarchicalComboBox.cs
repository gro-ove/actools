using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class SelectedItemChangedEventArgs : EventArgs {
        public object OldValue { get; }

        public object NewValue { get; }

        public SelectedItemChangedEventArgs(object oldValue, object newValue) {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public interface IHierarchicalItemPreviewProvider {
        [CanBeNull]
        object GetPreview([CanBeNull] object item);

        PlacementMode GetPlacementMode([CanBeNull] object item);
    }

    public interface IShortDisplayable {
        string ShortDisplayName { get; }
    }

    public class HierarchicalGroup : BetterObservableCollection<object> {
        public HierarchicalGroup() { }

        public HierarchicalGroup(string displayName, List<object> list) : base(list) {
            _displayName = displayName;
        }

        public HierarchicalGroup(string displayName, [NotNull] IEnumerable<object> collection) : base(collection) {
            _displayName = displayName;
        }

        public HierarchicalGroup(List<object> list) : base(list) { }

        public HierarchicalGroup([NotNull] IEnumerable<object> collection) : base(collection) { }

        public HierarchicalGroup(string displayName) {
            _displayName = displayName;
        }

        public IValueConverter HeaderConverter { get; set; }

        private string _displayName;

        public string DisplayName {
            get => _displayName;
            set {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }
    }

    public class LazyMenuItem : MenuItem {
        public static readonly DependencyProperty AutoPrepareProperty = DependencyProperty.Register(nameof(AutoPrepare), typeof(bool),
                typeof(LazyMenuItem), new PropertyMetadata(OnAutoPrepareChanged));

        public bool AutoPrepare {
            get => GetValue(AutoPrepareProperty) as bool? == true;
            set => SetValue(AutoPrepareProperty, value);
        }

        private static void OnAutoPrepareChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            if ((bool)e.NewValue) {
                (((LazyMenuItem)o).ItemsSource as HierarchicalItemsView)?.Prepare();
            }
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) {
            base.OnItemsSourceChanged(oldValue, newValue);
            if (AutoPrepare) {
                (ItemsSource as HierarchicalItemsView)?.Prepare();
            }
        }

        protected override void OnSubmenuOpened(RoutedEventArgs e) {
            if (ItemsSource is HierarchicalItemsView view) {
                view.Prepare();
                foreach (var item in view.OfType<LazyMenuItem>()) {
                    (item.ItemsSource as HierarchicalItemsView)?.Prepare();
                }
            }

            base.OnSubmenuOpened(e);
        }

        protected override void OnSubmenuClosed(RoutedEventArgs e) {
            if (ItemsSource is HierarchicalItemsView view) {
                view.Release();
                foreach (var item in view.OfType<LazyMenuItem>()) {
                    (item.ItemsSource as HierarchicalItemsView)?.Release();
                }
            }

            base.OnSubmenuClosed(e);
        }
    }

    public class HierarchicalItem : LazyMenuItem {
        [CanBeNull]
        private readonly HierarchicalItemsView _view;

        [CanBeNull]
        public object OriginalValue { get; }

        static HierarchicalItem() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HierarchicalItem), new FrameworkPropertyMetadata(typeof(HierarchicalItem)));
        }

        public HierarchicalItem() { }

        public HierarchicalItem([CanBeNull] HierarchicalItemsView parentView, [CanBeNull] object originalValue) {
            _view = parentView;
            OriginalValue = originalValue;
        }

        private bool _previewInitialized;

        protected override void OnMouseEnter(MouseEventArgs e) {
            base.OnMouseEnter(e);

            if (_previewInitialized || _view == null) return;
            _previewInitialized = true;

            if (_view.PreviewProvider != null) {
                SetValue(PreviewValuePropertyKey, _view.PreviewProvider.GetPreview(OriginalValue));
                SetValue(ToolTipPlacementPropertyKey, _view.PreviewProvider.GetPlacementMode(OriginalValue));
            }
        }

        public static readonly DependencyPropertyKey PreviewValuePropertyKey = DependencyProperty.RegisterReadOnly(nameof(PreviewValue), typeof(object),
                typeof(HierarchicalItem), new PropertyMetadata(null));

        public static readonly DependencyProperty PreviewValueProperty = PreviewValuePropertyKey.DependencyProperty;

        [CanBeNull]
        public object PreviewValue => GetValue(PreviewValueProperty);

        public static readonly DependencyPropertyKey ToolTipPlacementPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ToolTipPlacement),
                typeof(PlacementMode),
                typeof(HierarchicalItem), new PropertyMetadata(PlacementMode.Mouse));

        public static readonly DependencyProperty ToolTipPlacementProperty = ToolTipPlacementPropertyKey.DependencyProperty;

        public PlacementMode ToolTipPlacement => (PlacementMode)GetValue(ToolTipPlacementProperty);
    }

    public delegate void ItemChosenCallback(object item, [CanBeNull] HierarchicalGroup parentGroup);

    public class HierarchicalItemsView : BetterObservableCollection<UIElement>, IDisposable, ICommand {
        [CanBeNull]
        public HierarchicalComboBox Parent { get; }

        [CanBeNull]
        private WeakReference<HierarchicalComboBox> _temporaryParent;

        [CanBeNull]
        internal IHierarchicalItemPreviewProvider PreviewProvider => (_temporaryParent != null &&
                _temporaryParent.TryGetTarget(out var provider) ? provider : Parent)?.PreviewProvider;

        [NotNull]
        public ItemChosenCallback Handler { get; }

        [CanBeNull]
        private IList _source;

        [CanBeNull]
        private INotifyCollectionChanged _notify;

        private readonly bool _lazy;

        public HierarchicalItemsView([NotNull] ItemChosenCallback handler, [CanBeNull] IList source, bool lazy = true) {
            Handler = handler;

            _source = source;
            _notify = _source as INotifyCollectionChanged;
            _lazy = lazy;
            if (_notify != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(_notify, nameof(_notify.CollectionChanged),
                        OnItemsSourceCollectionChanged);
            }

            if (_lazy) {
                _obsolete = true;
            } else {
                Rebuild();
            }
        }

        public HierarchicalItemsView([NotNull] HierarchicalComboBox parent, [CanBeNull] IList source, bool lazy = true)
                : this(parent.ItemChosen, source, lazy) {
            Parent = parent;
        }

        private bool _prepared, _obsolete;

        public void Prepare() {
            if (_prepared) return;
            _prepared = true;

            if (_lazy && _obsolete) {
                Rebuild();
            }
        }

        public void Release() {
            _prepared = false;
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

            if (changed) {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }

            if (!_lazy || _prepared) {
                Rebuild();
            } else {
                _obsolete = true;
            }
        }

        [ValueConversion(typeof(int), typeof(Visibility))]
        private class InnerCountToVisibilityConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value.As<int>() > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static object GetValue(DependencyObject obj) {
            return obj.GetValue(ValueProperty);
        }

        public static void SetValue(DependencyObject obj, object value) {
            obj.SetValue(ValueProperty, value ?? NullSubstitute);
        }

        private static readonly object NullSubstitute = new object();

        public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(object),
                typeof(HierarchicalItemsView), new UIPropertyMetadata(null, null, CoerceMyProperty));

        private static object CoerceMyProperty(DependencyObject d, object baseValue) {
            return baseValue ?? NullSubstitute;
        }

        private UIElement Wrap([CanBeNull] object value, [CanBeNull] IValueConverter headerConverter) {
            if (value is UIElement direct) {
                if (value is MenuItem menuItem) {
                    var actualValue = GetValue(menuItem);
                    if (actualValue != null) {
                        menuItem.Command = this;
                        menuItem.CommandParameter = ReferenceEquals(actualValue, NullSubstitute) ? null : actualValue;
                    }
                }

                // return new TextBlock { Text = value.GetType().FullName };
                return direct;
            }

            var result = new HierarchicalItem(this, value);
            if (value == null) return result;

            var view = value as HierarchicalItemsView;

            if ((view?._source ?? value) is IShortDisplayable shortDisplayable) {
                result.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding {
                    Source = shortDisplayable,
                    Path = new PropertyPath(nameof(IShortDisplayable.ShortDisplayName)),
                    Converter = headerConverter,
                    Mode = BindingMode.OneWay
                });
            } else {
                result.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding {
                    Source = view?._source ?? value,
                    Path = new PropertyPath(nameof(Displayable.DisplayName)),
                    Converter = headerConverter,
                    Mode = BindingMode.OneWay
                });
            }

            if (view != null) {
                view._temporaryParent = Parent == null ? null : new WeakReference<HierarchicalComboBox>(Parent);

                result.ItemsSource = view;
                result.SetBinding(UIElement.VisibilityProperty, new Binding {
                    Source = view,
                    Path = new PropertyPath(nameof(view.Count)),
                    Mode = BindingMode.OneWay,
                    Converter = new InnerCountToVisibilityConverter()
                });
            } else {
                if (!(value is HierarchicalGroup group)) {
                    result.Command = this;
                    result.CommandParameter = value;
                } else {
                    result.ItemsSource = Parent == null ? new HierarchicalItemsView(Handler, group, _lazy) : new HierarchicalItemsView(Parent, group, _lazy);
                    result.SetBinding(UIElement.VisibilityProperty, new Binding {
                        Source = group,
                        Path = new PropertyPath(nameof(group.Count)),
                        Mode = BindingMode.OneWay,
                        Converter = new InnerCountToVisibilityConverter()
                    });
                }
            }

            return result;
        }

        public void Rebuild() {
            _obsolete = false;

            var converter = (_source as HierarchicalGroup)?.HeaderConverter;
            ReplaceEverythingBy(_source?.OfType<object>().Select(x => Wrap(x, converter)) ?? new HierarchicalItem[0]);
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            var source = _source;
            if (source == null) return;

            if (!_lazy || _prepared) {
                if (e.Action == NotifyCollectionChangedAction.Reset) {
                    Rebuild();
                } else {
                    if (e.OldItems != null) {
                        foreach (var o in e.OldItems) {
                            var removed = this.Skip(e.OldStartingIndex).OfType<HierarchicalItem>().FirstOrDefault(x => x.Tag == o);
                            if (removed == null) continue;

                            (removed.ItemsSource as HierarchicalItemsView)?.Dispose();
                            Remove(removed);
                        }
                    }

                    if (e.NewItems != null) {
                        var converter = (_source as HierarchicalGroup)?.HeaderConverter;
                        foreach (var n in e.NewItems) {
                            var index = source.IndexOf(n);
                            if (index < 0 || index >= source.Count - 1) {
                                Add(Wrap(n, converter));
                            } else {
                                Insert(index, Wrap(n, converter));
                            }
                        }
                    }
                }
            } else {
                _obsolete = true;
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
            Handler.Invoke(parameter, _source as HierarchicalGroup);
        }

        public event EventHandler CanExecuteChanged;
    }

    public class HierarchicalComboBox : Control {
        public HierarchicalComboBox() {
            DefaultStyleKey = typeof(HierarchicalComboBox);
            PreviewKeyDown += OnKeyDown;
        }

        private object GetItem(int offset) {
            var items = Flatten(ItemsSource).Where(x => !(x is UIElement)).ToList();
            var current = SelectedItem;
            if (items.Count < 2) return current;

            var index = items.IndexOf(current);
            if (index == -1) return items.FirstOrDefault();

            var newIndex = (index + offset % items.Count + items.Count) % items.Count;
            return items.ElementAtOrDefault(newIndex);
        }

        private void OnKeyDown(object sender, KeyEventArgs args) {
            if (_menu?.IsSubmenuOpen == true) return;

            switch (args.Key) {
                case Key.Down:
                case Key.Right:
                    SelectedItem = GetItem(1);
                    break;
                case Key.Up:
                case Key.Left:
                    SelectedItem = GetItem(-1);
                    break;
                default:
                    return;
            }

            args.Handled = true;
        }

        private LazyMenuItem _menu;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            _menu = GetTemplateChild("PART_MenuItem") as LazyMenuItem;
        }

        public static readonly DependencyProperty InnerMarginProperty = DependencyProperty.Register(nameof(InnerMargin), typeof(Thickness),
                typeof(HierarchicalComboBox));

        public Thickness InnerMargin {
            get => GetValue(InnerMarginProperty) as Thickness? ?? default;
            set => SetValue(InnerMarginProperty, value);
        }

        public event EventHandler<SelectedItemChangedEventArgs> SelectionChanged;

        public event EventHandler<SelectedItemChangedEventArgs> ItemSelected;

        public static readonly DependencyProperty SelectedContentProperty = DependencyProperty.Register(nameof(SelectedContent), typeof(DataTemplate),
                typeof(HierarchicalComboBox), new PropertyMetadata(OnSelectedContentChanged));

        public DataTemplate SelectedContent {
            get => (DataTemplate)GetValue(SelectedContentProperty);
            set => SetValue(SelectedContentProperty, value);
        }

        private static void OnSelectedContentChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HierarchicalComboBox)o).OnSelectedContentChanged((DataTemplate)e.OldValue, (DataTemplate)e.NewValue);
        }

        private void OnSelectedContentChanged(object oldValue, object newValue) {
            /*if (oldValue != null) {
                RemoveLogicalChild(oldValue);
            }

            if (newValue != null) {
                AddLogicalChild(newValue);
            }*/
        }

        public static readonly DependencyPropertyKey InnerItemsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(InnerItems),
                typeof(HierarchicalItemsView),
                typeof(HierarchicalComboBox), new PropertyMetadata(null));

        public static readonly DependencyProperty InnerItemsProperty = InnerItemsPropertyKey.DependencyProperty;

        internal HierarchicalItemsView InnerItems => (HierarchicalItemsView)GetValue(InnerItemsProperty);

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IList),
                typeof(HierarchicalComboBox), new PropertyMetadata(OnItemsSourceChanged));

        public IList ItemsSource {
            get => (IList)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HierarchicalComboBox)o).OnItemsSourceChanged((IList)e.NewValue);
        }

        private static IEnumerable<object> Flatten([NotNull] IList list) {
            foreach (var o in list) {
                if (o is HierarchicalGroup g) {
                    foreach (var c in Flatten(g)) {
                        yield return c;
                    }
                } else {
                    yield return o;
                }
            }
        }

        private static bool GetGroup([NotNull] IList list, object item, out HierarchicalGroup group) {
            foreach (var o in list) {
                if (!(o is HierarchicalGroup g)) {
                    if (o == item) {
                        group = list as HierarchicalGroup;
                        return true;
                    }
                } else if (GetGroup(g, item, out group)) {
                    return true;
                }
            }

            group = null;
            return false;
        }

        private static HierarchicalGroup GetGroup([NotNull] IList list, object item) {
            if (list == null) throw new ArgumentNullException(nameof(list));

            HierarchicalGroup g;
            return GetGroup(list, item, out g) ? g : null;
        }

        private void OnItemsSourceChanged([CanBeNull] IList newValue) {
            InnerItems?.Dispose();
            SetValue(InnerItemsPropertyKey, new HierarchicalItemsView(this, newValue));

            if (FixedMode) {
                if (newValue == null) {
                    SelectedItem = null;
                } else if (!Flatten(newValue).Contains(SelectedItem)) {
                    SelectedItem = Flatten(newValue).FirstOrDefault();
                    SetValue(SelectedItemHeaderConverterPropertyKey, GetGroup(newValue, SelectedItem)?.HeaderConverter);
                    return;
                }
            }

            if (newValue != null) {
                SetValue(SelectedItemHeaderConverterPropertyKey, GetGroup(newValue, SelectedItem)?.HeaderConverter);
            }
        }

        /// <summary>
        /// Update SelectedItem when ItemsSource value is changed.
        /// </summary>
        public static readonly DependencyProperty FixedModeProperty = DependencyProperty.Register(nameof(FixedMode), typeof(bool),
                typeof(HierarchicalComboBox), new FrameworkPropertyMetadata(true));

        public bool FixedMode {
            get => GetValue(FixedModeProperty) as bool? == true;
            set => SetValue(FixedModeProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object),
                typeof(HierarchicalComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HierarchicalComboBox)o).OnSelectedItemChanged(e.OldValue, e.NewValue);
        }

        private void OnSelectedItemChanged(object oldValue, object newValue) {
            SelectionChanged?.Invoke(this, new SelectedItemChangedEventArgs(oldValue, newValue));

            var itemsSource = ItemsSource;
            SetValue(SelectedItemHeaderConverterPropertyKey, itemsSource == null ? null : GetGroup(itemsSource, SelectedItem)?.HeaderConverter);
        }

        internal void ItemChosen(object item, HierarchicalGroup parentGroup) {
            SetValue(SelectedItemHeaderConverterPropertyKey, parentGroup?.HeaderConverter);

            var oldValue = SelectedItem;
            SelectedItem = item;
            ItemSelected?.Invoke(this, new SelectedItemChangedEventArgs(oldValue, item));
        }

        public static readonly DependencyPropertyKey SelectedItemHeaderConverterPropertyKey =
                DependencyProperty.RegisterReadOnly(nameof(SelectedItemHeaderConverter), typeof(IValueConverter),
                        typeof(HierarchicalComboBox), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedItemHeaderConverterProperty = SelectedItemHeaderConverterPropertyKey.DependencyProperty;

        public IValueConverter SelectedItemHeaderConverter => (IValueConverter)GetValue(SelectedItemHeaderConverterProperty);

        public object SelectedItem {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty PreviewProviderProperty = DependencyProperty.Register(nameof(PreviewProvider),
                typeof(IHierarchicalItemPreviewProvider), typeof(HierarchicalComboBox));

        [CanBeNull]
        public IHierarchicalItemPreviewProvider PreviewProvider {
            get => (IHierarchicalItemPreviewProvider)GetValue(PreviewProviderProperty);
            set => SetValue(PreviewProviderProperty, value);
        }
    }
}