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
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
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
    }

    public interface IShortDisplayable {
        string ShortDisplayName { get; }
    }

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

    internal class LazyMenuItem : MenuItem {
        public static readonly DependencyProperty AutoPrepareProperty = DependencyProperty.Register(nameof(AutoPrepare), typeof(bool),
                typeof(LazyMenuItem), new PropertyMetadata(OnAutoPrepareChanged));

        public bool AutoPrepare {
            get { return (bool)GetValue(AutoPrepareProperty); }
            set { SetValue(AutoPrepareProperty, value); }
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
            var view = ItemsSource as HierarchicalItemsView;
            if (view != null) {
                view.Prepare();
                foreach (var item in view.OfType<LazyMenuItem>()) {
                    (item.ItemsSource as HierarchicalItemsView)?.Prepare();
                }
            }

            base.OnSubmenuOpened(e);
        }

        protected override void OnSubmenuClosed(RoutedEventArgs e) {
            var view = ItemsSource as HierarchicalItemsView;
            if (view != null) {
                view.Release();
                foreach (var item in view.OfType<LazyMenuItem>()) {
                    (item.ItemsSource as HierarchicalItemsView)?.Release();
                }
            }

            base.OnSubmenuClosed(e);
        }
    }

    internal class HierarchicalItem : LazyMenuItem {
        [CanBeNull]
        private readonly HierarchicalItemsView _view;

        [CanBeNull]
        public object OriginalValue { get; }

        static HierarchicalItem() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HierarchicalItem), new FrameworkPropertyMetadata(typeof(HierarchicalItem)));
        }

        public HierarchicalItem() {}

        public HierarchicalItem([CanBeNull] HierarchicalItemsView parentView, [CanBeNull] object originalValue) {
            _view = parentView;
            OriginalValue = originalValue;
        }

        private bool _previewInitialized;

        protected override void OnMouseEnter(MouseEventArgs e) {
            base.OnMouseEnter(e);

            if (_previewInitialized || _view == null) return;
            _previewInitialized = true;

            SetValue(PreviewValuePropertyKey, _view.PreviewProvider?.GetPreview(OriginalValue));
        }

        public static readonly DependencyPropertyKey PreviewValuePropertyKey = DependencyProperty.RegisterReadOnly(nameof(PreviewValue), typeof(object),
                typeof(HierarchicalItem), new PropertyMetadata(null));

        public static readonly DependencyProperty PreviewValueProperty = PreviewValuePropertyKey.DependencyProperty;

        [CanBeNull]
        public object PreviewValue => GetValue(PreviewValueProperty);
    }

    public class HierarchicalItemsView : BetterObservableCollection<UIElement>, IDisposable, ICommand {
        [CanBeNull]
        public HierarchicalComboBox Parent { get; }

        [CanBeNull]
        private WeakReference<HierarchicalComboBox> _temporaryParent;

        [CanBeNull]
        internal IHierarchicalItemPreviewProvider PreviewProvider {
            get {
                HierarchicalComboBox provider;
                return (_temporaryParent != null && _temporaryParent.TryGetTarget(out provider) ? provider : Parent)?.PreviewProvider;
            }
        }

        [NotNull]
        public Action<object> Handler { get; }

        [CanBeNull]
        private IList _source;

        [CanBeNull]
        private INotifyCollectionChanged _notify;

        private readonly bool _lazy;

        public HierarchicalItemsView([NotNull] Action<object> handler, [CanBeNull] IList source, bool lazy = true) {
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

            var view = value as HierarchicalItemsView;

            var shortDisplayable = (view?._source ?? value) as IShortDisplayable;
            if (shortDisplayable != null) {
                result.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding {
                    Source = shortDisplayable,
                    Path = new PropertyPath(nameof(IShortDisplayable.ShortDisplayName)),
                    Mode = BindingMode.OneWay
                });
            } else {
                result.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding {
                    Source = view?._source ?? value,
                    Path = new PropertyPath(nameof(Displayable.DisplayName)),
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
                var group = value as HierarchicalGroup;
                if (group == null) {
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
            ReplaceEverythingBy(_source?.OfType<object>().Select(Wrap) ?? new HierarchicalItem[0]);
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (_source == null) return;
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
                        foreach (var n in e.NewItems) {
                            var index = _source.IndexOf(n);
                            if (index < 0 || index >= _source.Count - 1) {
                                Add(Wrap(n));
                            } else {
                                Insert(index, Wrap(n));
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
            Handler.Invoke(parameter);
        }

        public event EventHandler CanExecuteChanged;
    }

    public class HierarchicalComboBox : Control {
        public HierarchicalComboBox() {
            DefaultStyleKey = typeof(HierarchicalComboBox);
        }

        public static readonly DependencyProperty InnerMarginProperty = DependencyProperty.Register(nameof(InnerMargin), typeof(Thickness),
                typeof(HierarchicalComboBox));

        public Thickness InnerMargin {
            get { return (Thickness)GetValue(InnerMarginProperty); }
            set { SetValue(InnerMarginProperty, value); }
        }

        public event EventHandler<SelectedItemChangedEventArgs> SelectionChanged;

        public event EventHandler<SelectedItemChangedEventArgs> ItemSelected;

        public static readonly DependencyProperty SelectedContentProperty = DependencyProperty.Register(nameof(SelectedContent), typeof(DataTemplate),
                typeof(HierarchicalComboBox), new PropertyMetadata(OnSelectedContentChanged));

        public DataTemplate SelectedContent {
            get { return (DataTemplate)GetValue(SelectedContentProperty); }
            set { SetValue(SelectedContentProperty, value); }
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

        public static readonly DependencyPropertyKey InnerItemsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(InnerItems), typeof(HierarchicalItemsView),
                typeof(HierarchicalComboBox), new PropertyMetadata(null));

        public static readonly DependencyProperty InnerItemsProperty = InnerItemsPropertyKey.DependencyProperty;

        internal HierarchicalItemsView InnerItems => (HierarchicalItemsView)GetValue(InnerItemsProperty);

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
            SetValue(InnerItemsPropertyKey, new HierarchicalItemsView(this, newValue));

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
                typeof(HierarchicalComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HierarchicalComboBox)o).OnSelectedItemChanged(e.OldValue, e.NewValue);
        }

        private void OnSelectedItemChanged(object oldValue, object newValue) {
            SelectionChanged?.Invoke(this, new SelectedItemChangedEventArgs(oldValue, newValue));
        }

        internal void ItemChosen(object item) {
            var oldValue = SelectedItem;
            SelectedItem = item;
            ItemSelected?.Invoke(this, new SelectedItemChangedEventArgs(oldValue, item));
        }

        public object SelectedItem {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly DependencyProperty PreviewProviderProperty = DependencyProperty.Register(nameof(PreviewProvider),
                typeof(IHierarchicalItemPreviewProvider), typeof(HierarchicalComboBox));

        [CanBeNull]
        public IHierarchicalItemPreviewProvider PreviewProvider {
            get { return (IHierarchicalItemPreviewProvider)GetValue(PreviewProviderProperty); }
            set { SetValue(PreviewProviderProperty, value); }
        }
    }
}