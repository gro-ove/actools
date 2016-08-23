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
using FirstFloor.ModernUI.Helpers;
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
        protected override void OnSubmenuOpened(RoutedEventArgs e) {
            var view = ItemsSource as MenuItemsView;
            if (view != null) {
                view.Prepare();
                foreach (var item in view.OfType<LazyMenuItem>()) {
                    (item.ItemsSource as MenuItemsView)?.Prepare();
                }
            }

            base.OnSubmenuOpened(e);
        }

        protected override void OnSubmenuClosed(RoutedEventArgs e) {
            var view = ItemsSource as MenuItemsView;
            if (view != null) {
                view.Release();
                foreach (var item in view.OfType<LazyMenuItem>()) {
                    (item.ItemsSource as MenuItemsView)?.Release();
                }
            }

            base.OnSubmenuClosed(e);
        }
    }

    internal class HierarchicalItem : LazyMenuItem {
        [NotNull]
        public MenuItemsView ParentView { get; }

        [CanBeNull]
        public object OriginalValue { get; }

        static HierarchicalItem() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HierarchicalItem), new FrameworkPropertyMetadata(typeof(HierarchicalItem)));
        }

        public HierarchicalItem([NotNull] MenuItemsView parentView, object originalValue) {
            ParentView = parentView;
            OriginalValue = originalValue;
        }

        private bool _previewInitialized;

        protected override void OnMouseEnter(MouseEventArgs e) {
            base.OnMouseEnter(e);

            if (_previewInitialized) return;
            _previewInitialized = true;

            SetValue(PreviewValuePropertyKey, ParentView.Parent.PreviewProvider?.GetPreview(OriginalValue));
        }

        public static readonly DependencyPropertyKey PreviewValuePropertyKey = DependencyProperty.RegisterReadOnly(nameof(PreviewValue), typeof(object),
                typeof(HierarchicalItem), new PropertyMetadata(0));

        public static readonly DependencyProperty PreviewValueProperty = PreviewValuePropertyKey.DependencyProperty;

        [CanBeNull]
        public object PreviewValue => GetValue(PreviewValueProperty);
    }

    internal class MenuItemsView : BetterObservableCollection<UIElement>, IDisposable, ICommand {
        [NotNull]
        public HierarchicalComboBox Parent { get; }

        [CanBeNull]
        private IList _source;

        [CanBeNull]
        private INotifyCollectionChanged _notify;

        public MenuItemsView([NotNull] HierarchicalComboBox parent) {
            Parent = parent;
        }

        public MenuItemsView([NotNull] HierarchicalComboBox parent, [CanBeNull] IList source) {
            Parent = parent;

            _source = source;
            _notify = _source as INotifyCollectionChanged;
            if (_notify != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(_notify, nameof(_notify.CollectionChanged),
                        OnItemsSourceCollectionChanged);
            }

            _obsolete = true;
        }

        private bool _prepared;
        private bool _obsolete;

        public void Prepare() {
            if (_prepared) return;
            _prepared = true;

            if (_obsolete) {
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

            if (_prepared) {
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
            Logging.Debug("Wrap()");

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
                result.ItemsSource = new MenuItemsView(Parent, group);
                result.SetBinding(UIElement.VisibilityProperty, new Binding {
                    Source = group,
                    Path = new PropertyPath(nameof(HierarchicalGroup.Count)),
                    Mode = BindingMode.OneWay,
                    Converter = new InnerCountToVisibilityConverter()
                });
            }

            return result;
        }

        public void Rebuild() {
            _obsolete = false;
            ReplaceEverythingBy(_source?.OfType<object>().Select(Wrap) ?? new HierarchicalItem[0]);
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (_source == null || e.Action == NotifyCollectionChangedAction.Reset) {
                return;
            }
            if (_prepared) {
                if (e.Action == NotifyCollectionChangedAction.Reset) {
                    Rebuild();
                } else {
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
            Parent.SelectedItem = parameter;
        }

        public event EventHandler CanExecuteChanged;
    }

    public class HierarchicalComboBox : Control {
        public HierarchicalComboBox() {
            DefaultStyleKey = typeof(HierarchicalComboBox);
        }

        public event EventHandler<SelectedItemChangedEventArgs> SelectionChanged;

        public static readonly DependencyProperty SelectedContentProperty = DependencyProperty.Register(nameof(SelectedContent), typeof(object),
                typeof(HierarchicalComboBox));

        public object SelectedContent {
            get { return GetValue(SelectedContentProperty); }
            set { SetValue(SelectedContentProperty, value); }
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
                typeof(HierarchicalComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HierarchicalComboBox)o).OnSelectedItemChanged(e.OldValue, e.NewValue);
        }

        private void OnSelectedItemChanged(object oldValue, object newValue) {
            SelectionChanged?.Invoke(this, new SelectedItemChangedEventArgs(oldValue, newValue));
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

        public static readonly DependencyProperty ShowPreviewProperty = DependencyProperty.Register(nameof(ShowPreview), typeof(bool),
                typeof(HierarchicalComboBox));

        public bool ShowPreview {
            get { return (bool)GetValue(ShowPreviewProperty); }
            set { SetValue(ShowPreviewProperty, value); }
        }
    }
}