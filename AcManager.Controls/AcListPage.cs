using System;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;

namespace AcManager.Controls {
    public class AcListPage : Control {
        static AcListPage() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcListPage), new FrameworkPropertyMetadata(typeof(AcListPage)));
        }

        public bool SelectMode { get; set; }

        public event EventHandler<AcObjectEventArgs> SelectedAcObjectChanged;
        private ListBox _list;

        private readonly DelayedPropertyWrapper<AcItemWrapper> _selectedWrapper;
        private AcObjectNew _currentObject;

        public AcListPage() {
            SelectMode = false;

            _selectedWrapper = new DelayedPropertyWrapper<AcItemWrapper>(v => {
                if (v != null) {
                    _list?.ScrollIntoView(v);
                }

                if (_currentObject != null) {
                    _currentObject.AcObjectOutdated -= SelectedAcObject_Outdated;
                }

                _currentObject = v?.Loaded();

                if (_currentObject != null) {
                    _currentObject.AcObjectOutdated += SelectedAcObject_Outdated;
                    
                    SetCurrentValue(SelectedSourceProperty, AcObjectsUriManager.GetUri(_currentObject));
                    SelectedAcObjectChanged?.Invoke(this, new AcObjectEventArgs(_currentObject));
                } else {
                    SetCurrentValue(SelectedSourceProperty, null);
                }
            });
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            _list = GetTemplateChild("ItemsList") as ListBox;
            _list?.ScrollIntoView(_list.SelectedItem);
        }

        private void ItemsSource_CurrentChanged(object sender, EventArgs e) {
            var newValue = ItemsSource.CurrentItem as AcItemWrapper;
            if (ItemsSource.Contains(_selectedWrapper.Value)) {
                _selectedWrapper.Value = newValue;
            } else {
                _selectedWrapper.ForceValue(newValue);
            }
        }

        private void SelectedAcObject_Outdated(object sender, EventArgs e) {
            ItemsSource.MoveCurrentTo(null);
        }

        #region Control Properies
        public static readonly DependencyProperty SelectedSourceProperty = DependencyProperty.Register("SelectedSource", typeof(Uri),
            typeof(AcListPage), new PropertyMetadata());

        public Uri SelectedSource => (Uri)GetValue(SelectedSourceProperty);

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(AcWrapperCollectionView),
            typeof(AcListPage), new PropertyMetadata(OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcListPage)d).OnItemsSourceChanged((AcWrapperCollectionView)e.OldValue, (AcWrapperCollectionView)e.NewValue);
        }

        private void OnItemsSourceChanged(AcWrapperCollectionView oldValue, AcWrapperCollectionView newValue) {
            if (oldValue != null) {
                oldValue.CurrentChanged -= ItemsSource_CurrentChanged;
            }

            if (newValue != null) {
                newValue.CurrentChanged += ItemsSource_CurrentChanged;
                _selectedWrapper.Value = newValue.CurrentItem as AcItemWrapper;
            }
        }

        public AcWrapperCollectionView ItemsSource {
            get { return (AcWrapperCollectionView)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }
        #endregion
    }

    public class AcObjectEventArgs
        : EventArgs {
        public AcObjectEventArgs(AcObjectNew obj) {
            AcObject = obj;
        }

        public AcObjectNew AcObject { get; }
    }
}
