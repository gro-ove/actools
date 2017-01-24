using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
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
                    SetCurrentValue(SelectedSourceProperty, AcObjectsUriManager.GetUri(_currentObject));
                } else {
                    SetCurrentValue(SelectedSourceProperty, null);
                }
            }
        }

        public AcListPage() {
            SelectMode = false;

            _selectedWrapper = new DelayedPropertyWrapper<AcItemWrapper>(async v => {
                CurrentObject = v?.Loaded();

                if (v != null && SettingsHolder.Content.ScrollAutomatically) {
                    await Task.Delay(1);
                    _list?.ScrollIntoView(_list.SelectedItem);
                }
            });
        }

        public static readonly DependencyProperty AddNewCommandProperty = DependencyProperty.Register(nameof(AddNewCommand), typeof(ICommand),
                typeof(AcListPage), new PropertyMetadata(OnAddNewCommandChanged));

        public ICommand AddNewCommand {
            get { return (ICommand)GetValue(AddNewCommandProperty); }
            set { SetValue(AddNewCommandProperty, value); }
        }

        private static void OnAddNewCommandChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((AcListPage)o).OnAddNewCommandChanged((ICommand)e.NewValue);
        }

        private void OnAddNewCommandChanged(ICommand newValue) {
            InputBindings.Clear();
            InputBindings.Add(new InputBinding(newValue, new KeyGesture(Key.N, ModifierKeys.Control | ModifierKeys.Shift)));
        }

        private Button _addButton;

        public override void OnApplyTemplate() {
            if (_addButton != null) {
                _addButton.Click -= OnAddButtonClick;
            }

            base.OnApplyTemplate();
            _list = GetTemplateChild(@"ItemsList") as ListBox;
            _list?.ScrollIntoView(_list.SelectedItem);
            _addButton = GetTemplateChild(@"AddCarButton") as Button;

            if (_addButton != null) {
                _addButton.Click += OnAddButtonClick;
            }
        }

        private void OnAddButtonClick(object sender, RoutedEventArgs e) {
            var command = AddNewCommand;
            if (command?.CanExecute(null) == true) {
                var list = ItemsSource as INotifyCollectionChanged;
                if (list != null) {
                    list.CollectionChanged += OnAddButtonCollectionChanged;
                }

                command.Execute(null);

                if (list != null) {
                    list.CollectionChanged -= OnAddButtonCollectionChanged;
                }
            }
        }

        private void OnAddButtonCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems.Count == 1) {
                ItemsSource.MoveCurrentTo(e.NewItems[0]);
            }
        }

        private void OnCurrentChanged(object sender, EventArgs e) {
            var newValue = ItemsSource.CurrentItem as AcItemWrapper;
            if (ItemsSource.Contains(_selectedWrapper.Value)) {
                _selectedWrapper.Value = newValue;
            } else {
                _selectedWrapper.ForceValue(newValue);
            }
        }

        private void SelectedAcObject_Outdated(object sender, EventArgs e) {
            SetCurrentValue(SelectedSourceProperty, null);

            var oldItem = _selectedWrapper.Value;
            var id = (sender as AcObjectNew)?.Id;
            if (id == null) return;

            var newItem = ItemsSource.OfType<AcItemWrapper>().GetByIdOrDefault(id);
            if (oldItem != null && newItem == oldItem) {
                CurrentObject = newItem.Loaded();
            } else if (newItem != null) {
                ItemsSource.MoveCurrentTo(newItem);
            } else {
                var replacement = ItemsSource.OfType<AcItemWrapper>().FirstOrDefault(x => (x.Value as AcCommonObject)?.PreviousId == id);
                if (replacement != null) {
                    ItemsSource.MoveCurrentTo(replacement);
                    return;
                }

                if (CurrentObject != null) {
                    SetCurrentValue(SelectedSourceProperty, AcObjectsUriManager.GetUri(CurrentObject));
                } else {
                    ItemsSource.MoveCurrentToFirst();
                }
            }
        }

        #region Control Properies
        public static readonly DependencyProperty SelectedSourceProperty = DependencyProperty.Register(nameof(SelectedSource), typeof(Uri),
            typeof(AcListPage), new PropertyMetadata());

        public Uri SelectedSource => (Uri)GetValue(SelectedSourceProperty);

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(AcWrapperCollectionView),
            typeof(AcListPage), new PropertyMetadata(OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcListPage)d).OnItemsSourceChanged((AcWrapperCollectionView)e.OldValue, (AcWrapperCollectionView)e.NewValue);
        }

        private void OnItemsSourceChanged(AcWrapperCollectionView oldValue, AcWrapperCollectionView newValue) {
            if (oldValue != null) {
                oldValue.CurrentChanged -= OnCurrentChanged;
            }

            if (newValue != null) {
                newValue.CurrentChanged += OnCurrentChanged;
                _selectedWrapper.Value = newValue.CurrentItem as AcItemWrapper;
            }
        }

        public AcWrapperCollectionView ItemsSource {
            get { return (AcWrapperCollectionView)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }
        #endregion
    }

    public class AcObjectEventArgs : EventArgs {
        public AcObjectEventArgs(AcObjectNew obj) {
            AcObject = obj;
        }

        public AcObjectNew AcObject { get; }
    }
}
