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
            get { return (string)GetValue(BasicFilterProperty); }
            set { SetValue(BasicFilterProperty, value); }
        }

        public static readonly DependencyProperty UserFilterProperty = DependencyProperty.Register(nameof(UserFilter), typeof(string),
            typeof(AcObjectListBox), new PropertyMetadata(OnFilterChanged));

        public string UserFilter {
            get { return (string)GetValue(UserFilterProperty); }
            set { SetValue(UserFilterProperty, value); }
        }

        private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcObjectListBox)d).UpdateFilter();
        }

        public static readonly DependencyProperty IsFilteringEnabledProperty = DependencyProperty.Register(nameof(IsFilteringEnabled), typeof(bool),
            typeof(AcObjectListBox), new PropertyMetadata(true));

        public bool IsFilteringEnabled {
            get { return (bool)GetValue(IsFilteringEnabledProperty); }
            set { SetValue(IsFilteringEnabledProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(AcObjectNew),
            typeof(AcObjectListBox), new PropertyMetadata(OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcObjectListBox)d).OnSelectedItemChanged((AcObjectNew)e.NewValue);
        }

        private void OnSelectedItemChanged(AcObjectNew newValue) {
            if (InnerItemsSource == null || InnerItemsSource.CurrentItem == newValue) return;

            InnerItemsSource.MoveCurrentToOrNull(newValue);
            UpdateSelected();
        }

        public AcObjectNew SelectedItem {
            get { return (AcObjectNew)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IAcObjectList),
            typeof(AcObjectListBox), new PropertyMetadata(OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcObjectListBox)d).OnItemsSourceChanged((IAcObjectList)e.NewValue);
        }

        public static readonly DependencyProperty InnerItemsSourceProperty = DependencyProperty.Register(nameof(InnerItemsSource), typeof(AcWrapperCollectionView),
            typeof(AcObjectListBox), new PropertyMetadata());

        public IAcObjectList ItemsSource {
            get { return (IAcObjectList)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public AcWrapperCollectionView InnerItemsSource {
            get { return (AcWrapperCollectionView)GetValue(InnerItemsSourceProperty); }
            set { SetValue(InnerItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode),
                typeof(AcObjectListBox), new PropertyMetadata(SelectionMode.Single));

        public SelectionMode SelectionMode {
            get { return (SelectionMode)GetValue(SelectionModeProperty); }
            set { SetValue(SelectionModeProperty, value); }
        }

        [NotNull]
        public IEnumerable<AcObjectNew> GetSelectedItems() {
            return (GetTemplateChild(@"PART_ListBox") as ListBox)?.SelectedItems.OfType<AcObjectNew>() ?? new AcObjectNew[0];
        }

        public AcObjectListBox() {
            Loaded += AcObjectListBox_Loaded;
        }

        private bool _unloaded;

        private void AcObjectListBox_Loaded(object sender, RoutedEventArgs e) {
            if (_unloaded) {
                _unloaded = false;
                UpdateFilter();
            } else {
                Unloaded += AcObjectListBox_Unloaded;
            }
        }

        internal void AcObjectListBox_Unloaded(object sender, RoutedEventArgs e) {
            if (_unloaded) return;
            _unloaded = true;
            ClearFilter();
        }

        private IFilter<AcObjectNew> _filter;
        private IAcObjectList _observableCollection;

        private void OnItemsSourceChanged(IAcObjectList newValue) {
            ClearFilter();

            if (InnerItemsSource != null) {
                InnerItemsSource.CurrentChanged -= ItemsSource_CurrentChanged;
            }
            
            _observableCollection = newValue;
            if (newValue == null) return;

            InnerItemsSource = new AcWrapperCollectionView(_observableCollection) { CustomSort = this };
            InnerItemsSource.CurrentChanged += ItemsSource_CurrentChanged;
            UpdateFilter();
        }

        private void ClearFilter() {
            if (_filter != null) {
                _observableCollection.ItemPropertyChanged -= Collection_ItemPropertyChanged;
                _observableCollection.WrappedValueChanged -= Collection_WrappedValueChanged;
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

            InnerItemsSource.CurrentChanged -= ItemsSource_CurrentChanged;
            using (listView.DeferRefresh()) {
                if (string.IsNullOrWhiteSpace(filter)) {
                    listView.Filter = null;
                } else {
                    _filter = Filter.Create(UniqueAcObjectTester.Instance, filter);
                    listView.Filter = FilterFunc;
                    _observableCollection.ItemPropertyChanged += Collection_ItemPropertyChanged;
                    _observableCollection.WrappedValueChanged += Collection_WrappedValueChanged;
                }
            }
            InnerItemsSource.CurrentChanged += ItemsSource_CurrentChanged;

            listView.MoveCurrentToOrNull(selectObject);
        }

        private void UpdateFilter() {
            UpdateFilter(SelectedItem);
        }

        private void Collection_WrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            if (_filter == null) return;
            _observableCollection.RefreshFilter((AcItemWrapper)sender);
        }

        private void Collection_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
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

        private void ItemsSource_CurrentChanged(object sender, EventArgs e) {
            UpdateSelected();
        }

        private void UpdateSelected() {
            var selected = InnerItemsSource.LoadedCurrent;
            if (selected == null) return;
            
            SelectedItem = selected;
            SelectedAcObjectChanged?.Invoke(this, new AcObjectEventArgs(selected));
        }

        public event EventHandler<AcObjectEventArgs> SelectedAcObjectChanged;
    }
}
