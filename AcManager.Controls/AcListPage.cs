using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;

namespace AcManager.Controls {
    public class AcListPage : Control {
        static AcListPage() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcListPage), new FrameworkPropertyMetadata(typeof(AcListPage)));
        }

        public bool SelectMode { get; set; }
        
        private ListBox _list;

        private readonly DelayedPropertyWrapper<AcItemWrapper> _selectedWrapper;
        private AcObjectNew _currentObject;

        public AcObjectNew CurrentObject {
            get { return _currentObject; }
            set {
                if (_currentObject != null) {
                    _currentObject.AcObjectOutdated -= SelectedAcObject_Outdated;
                }

                _currentObject = value;

                if (_currentObject != null) {
                    _currentObject.AcObjectOutdated += SelectedAcObject_Outdated;
                    SetCurrentValue(SelectedSourceProperty, _setNextToNull ? null : AcObjectsUriManager.GetUri(_currentObject));
                } else {
                    SetCurrentValue(SelectedSourceProperty, null);
                }
            }
        }

        private bool _setNextToNull;

        public AcListPage() {
            SelectMode = false;

            _selectedWrapper = new DelayedPropertyWrapper<AcItemWrapper>(v => {
                if (v != null) {
                    _list?.ScrollIntoView(v);
                }
                
                CurrentObject = v?.Loaded();
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

        private async void SelectedAcObject_Outdated(object sender, EventArgs e) {
            SetCurrentValue(SelectedSourceProperty, null);

            var oldItem = _selectedWrapper.Value;
            var id = (sender as AcObjectNew)?.Id;
            if (id == null) {
                _setNextToNull = false;
                return;
            }

            _setNextToNull = true;
            await Task.Delay(1);

            _setNextToNull = false;
            var newItem = ItemsSource.OfType<AcItemWrapper>().GetByIdOrDefault(id);
            if (oldItem != null && newItem == oldItem) {
                CurrentObject = newItem.Loaded();
            } else if (newItem != null) {
                ItemsSource.MoveCurrentTo(newItem);
            } else if (CurrentObject != null) {
                SetCurrentValue(SelectedSourceProperty, AcObjectsUriManager.GetUri(CurrentObject));
            } else {
                ItemsSource.MoveCurrentToFirst();
            }
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
