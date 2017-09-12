using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Lists;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls {
    public class AcObjectListBox : Control, IComparer {
        static AcObjectListBox() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcObjectListBox), new FrameworkPropertyMetadata(typeof(AcObjectListBox)));
        }

        public static readonly DependencyProperty BasicFilterProperty = DependencyProperty.Register(nameof(BasicFilter), typeof(string),
            typeof(AcObjectListBox), new PropertyMetadata(OnFilterChanged));

        public string BasicFilter {
            get => (string)GetValue(BasicFilterProperty);
            set => SetValue(BasicFilterProperty, value);
        }

        public static readonly DependencyProperty UserFilterProperty = DependencyProperty.Register(nameof(UserFilter), typeof(string),
            typeof(AcObjectListBox), new PropertyMetadata(OnFilterChanged));

        public string UserFilter {
            get => (string)GetValue(UserFilterProperty);
            set => SetValue(UserFilterProperty, value);
        }

        public static readonly DependencyProperty UserFiltersKeyProperty = DependencyProperty.Register(nameof(UserFiltersKey), typeof(string),
                typeof(AcObjectListBox), new PropertyMetadata(@"AcObjectListBox:FiltersHistory"));

        public string UserFiltersKey {
            get => (string)GetValue(UserFiltersKeyProperty);
            set => SetValue(UserFiltersKeyProperty, value);
        }

        private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcObjectListBox)d).UpdateFilter();
        }

        public static readonly DependencyProperty IsFilteringEnabledProperty = DependencyProperty.Register(nameof(IsFilteringEnabled), typeof(bool),
            typeof(AcObjectListBox), new PropertyMetadata(true));

        public bool IsFilteringEnabled {
            get => GetValue(IsFilteringEnabledProperty) as bool? == true;
            set => SetValue(IsFilteringEnabledProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(AcObjectNew),
            typeof(AcObjectListBox), new PropertyMetadata(OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcObjectListBox)d).OnSelectedItemChanged((AcObjectNew)e.NewValue);
        }

        private void OnSelectedItemChanged(AcObjectNew newValue) {
            if (InnerItemsSource == null) return;

            if (InnerItemsSource.CurrentItem != newValue) {
                InnerItemsSource.MoveCurrentToOrNull(newValue);
                UpdateSelected();
            }

            if (SelectionMode != SelectionMode.Single && _listBox != null) {
                _ignoreSelectionChange = true;
                _listBox.SelectedItems.Clear();
                _listBox.SelectedItems.Add(InnerItemsSource.CurrentItem);
                _ignoreSelectionChange = false;
            }
        }

        public AcObjectNew SelectedItem {
            get => (AcObjectNew)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IAcObjectList),
            typeof(AcObjectListBox), new PropertyMetadata(OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcObjectListBox)d).OnItemsSourceChanged((IAcObjectList)e.NewValue);
        }

        public static readonly DependencyProperty InnerItemsSourceProperty = DependencyProperty.Register(nameof(InnerItemsSource), typeof(AcWrapperCollectionView),
            typeof(AcObjectListBox), new PropertyMetadata());

        public IAcObjectList ItemsSource {
            get => (IAcObjectList)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public AcWrapperCollectionView InnerItemsSource {
            get => (AcWrapperCollectionView)GetValue(InnerItemsSourceProperty);
            set => SetValue(InnerItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode),
                typeof(AcObjectListBox), new PropertyMetadata(SelectionMode.Single));

        public SelectionMode SelectionMode {
            get => GetValue(SelectionModeProperty) as SelectionMode? ?? SelectionMode.Single;
            set => SetValue(SelectionModeProperty, value);
        }

        public AcObjectListBox() {
            Loaded += OnLoaded;
        }

        private bool _unloaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_unloaded) {
                _unloaded = false;
                UpdateFilter();
            } else {
                Unloaded += OnUnloaded;
            }

            FocusTextBox();
        }

        private void FocusTextBox() {
            (GetTemplateChild("PART_FilterTextBox") as IInputElement)?.Focus();
        }

        internal void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_unloaded) return;
            _unloaded = true;
            ClearFilter();
        }

        private IFilter<AcObjectNew> _filter;
        private IAcObjectList _observableCollection;

        private void OnItemsSourceChanged(IAcObjectList newValue) {
            ClearFilter();

            if (InnerItemsSource != null) {
                InnerItemsSource.CurrentChanged -= OnItemsSourceCurrentChanged;
            }

            _observableCollection = newValue;
            if (newValue == null) return;

            InnerItemsSource = new AcWrapperCollectionView(_observableCollection) { CustomSort = this };
            InnerItemsSource.CurrentChanged += OnItemsSourceCurrentChanged;
            UpdateFilter();
        }

        private void ClearFilter() {
            if (_filter != null) {
                _observableCollection.ItemPropertyChanged -= OnCollectionItemPropertyChanged;
                _observableCollection.WrappedValueChanged -= OnCollectionWrappedValueChanged;
                _filter = null;
            }
        }

        private void UpdateFilter(IAcObjectNew selectObject) {
            ClearFilter();

            var listView = InnerItemsSource;
            if (listView == null) return;

            var baseFilter = BasicFilter;
            var userFilter = UserFilter;

            var filter = string.IsNullOrWhiteSpace(baseFilter) ? userFilter
                : string.IsNullOrWhiteSpace(userFilter) ? baseFilter : baseFilter + @" & (" + userFilter + @")";

            InnerItemsSource.CurrentChanged -= OnItemsSourceCurrentChanged;
            using (listView.DeferRefresh()) {
                if (string.IsNullOrWhiteSpace(filter)) {
                    listView.Filter = null;
                } else {
                    _filter = Filter.Create(UniversalAcObjectTester.Instance, filter);
                    listView.Filter = FilterFunc;
                    _observableCollection.ItemPropertyChanged += OnCollectionItemPropertyChanged;
                    _observableCollection.WrappedValueChanged += OnCollectionWrappedValueChanged;
                }
            }
            InnerItemsSource.CurrentChanged += OnItemsSourceCurrentChanged;

            listView.MoveCurrentToOrNull(selectObject);
        }

        private void UpdateFilter() {
            UpdateFilter(SelectedItem);
        }

        private void OnCollectionWrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            if (_filter == null) return;
            _observableCollection.RefreshFilter((AcItemWrapper)sender);
        }

        private void OnCollectionItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_filter == null || e.PropertyName != string.Empty && !_filter.IsAffectedBy(e.PropertyName)) return;
            _observableCollection.RefreshFilter((AcPlaceholderNew)sender);
        }

        public int Compare(object x, object y) {
            return AcItemWrapper.CompareHelper(x, y);
        }

        private bool FilterFunc(object o) {
            if (_filter == null || o == null) return false;
            var wrapper = o as AcItemWrapper;
            return wrapper?.IsLoaded == true && _filter.Test((AcObjectNew)wrapper.Value);
        }

        private void OnItemsSourceCurrentChanged(object sender, EventArgs e) {
            UpdateSelected();
        }

        private void UpdateSelected() {
            var selected = InnerItemsSource.LoadedCurrent;
            if (selected == null) return;

            SelectedItem = selected;
            SelectedAcObjectChanged?.Invoke(this, new AcObjectEventArgs(selected));
        }

        public event EventHandler<AcObjectEventArgs> SelectedAcObjectChanged;

        private ListBox _listBox;

        public override void OnApplyTemplate() {
            if (_listBox != null) {
                _listBox.SelectionChanged -= OnListBoxSelectionChanged;
            }

            base.OnApplyTemplate();

            _listBox = GetTemplateChild(@"PART_ListBox") as ListBox;
            if (_listBox != null) {
                if (SelectionMode != SelectionMode.Single && InnerItemsSource != null) {
                    _listBox.SelectedItems.Clear();
                    _listBox.SelectedItems.Add(InnerItemsSource.CurrentItem);
                }

                _listBox.SelectionChanged += OnListBoxSelectionChanged;
            }
        }

        private bool _ignoreSelectionChange;

        private void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_ignoreSelectionChange || SelectionMode == SelectionMode.Single) return;
            SelectedItem = (_listBox?.SelectedItem as AcItemWrapper)?.Loaded();
        }

        [NotNull]
        public IEnumerable<AcObjectNew> GetSelectedItems() {
            if (SelectionMode == SelectionMode.Single) return new[] { SelectedItem };
            return _listBox?.SelectedItems.OfType<AcItemWrapper>().Select(x => x.Loaded()) ??
                    new AcObjectNew[0];
        }
    }
}
