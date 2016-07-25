using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;

namespace AcManager.Pages.Dialogs {
    public partial class SelectCarDialog : INotifyPropertyChanged {
        public const string UriKey = "SelectAndSetupCarDialog.UriKey";

        private static WeakReference<SelectCarDialog> _instance;

        public static SelectCarDialog Instance {
            get {
                if (_instance == null) {
                    return null;
                }

                SelectCarDialog result;
                return _instance.TryGetTarget(out result) ? result : null;
            }
        }

        private CarSkinObject _selectedSkin;

        private readonly DelayedPropertyWrapper<CarObject> _selectedCar;

        public CarObject SelectedCar {
            get { return _selectedCar.Value; }
            set { _selectedCar.Value = value ?? SelectedCar; }
        }

        private CarObject _selectedTunableVersion;

        public CarObject SelectedTunableVersion {
            get { return _selectedTunableVersion; }
            set {
                if (Equals(value, _selectedTunableVersion)) return;
                _selectedTunableVersion = value;
                OnPropertyChanged();

                SelectedCar = value;
            }
        }

        private void SelectedCarChanged(CarObject value) {
            if (value != null) {
                value.SkinsManager.EnsureLoadedAsync().Forget();
                UpdateTunableVersions();
            }

            SelectedTunableVersion = value;
            OnPropertyChanged(nameof(SelectedCar));
            CommandManager.InvalidateRequerySuggested();

            if (value != null) {
                SelectedSkin = value.SelectedSkin;
            }

            if (_list != null) {
                _list.SelectedItem = value;
            }
        }

        private CarObject _previousTunableParent;

        private void UpdateTunableVersions() {
            if (_selectedCar == null) {
                TunableVersions.Clear();
                HasChildren = false;
                _previousTunableParent = null;
                return;
            }

            var parent = SelectedCar.ParentId == null ? SelectedCar : SelectedCar.Parent;
            if (parent == _previousTunableParent) {
                return;
            }

            if (parent == null) {
                TunableVersions.Clear();
                HasChildren = false;
                _previousTunableParent = null;
                return;
            }

            _previousTunableParent = parent;
            
            var children = parent.Children.Where(x => x.Enabled).ToList();
            HasChildren = children.Any();
            if (!HasChildren) {
                TunableVersions.Clear();
                return;
            }

            TunableVersions.ReplaceEverythingBy(new [] { parent }.Where(x => x.Enabled).Union(children));
            if (SelectedTunableVersion == null) {
                SelectedTunableVersion = SelectedCar;
            }
        }

        public CarSkinObject SelectedSkin {
            get { return _selectedSkin; }
            set {
                if (Equals(value, _selectedSkin)) return;
                _selectedSkin = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _hasChildren;

        public bool HasChildren {
            get { return _hasChildren; }
            set {
                if (Equals(value, _hasChildren)) return;
                _hasChildren = value;
                OnPropertyChanged();
            }
        }

        public BetterObservableCollection<CarObject> TunableVersions { get; } = new BetterObservableCollection<CarObject>();

        private RelayCommand _manageSetupsCommand;

        public RelayCommand ManageSetupsCommand => _manageSetupsCommand ?? (_manageSetupsCommand = new RelayCommand(o => {
            if (_selectedCar.Value == null) return;
            new CarSetupsDialog(_selectedCar.Value) {
                ShowInTaskbar = false
            }.ShowDialogWithoutBlocking();
        }));

        public SelectCarDialog(CarObject car) {
            _selectedCar = new DelayedPropertyWrapper<CarObject>(SelectedCarChanged);

            SelectedCar = car;
            _instance = new WeakReference<SelectCarDialog>(this);

            InitializeComponent();
            DataContext = this;

            Tabs.SelectedSource = ValuesStorage.GetUri(UriKey) ?? Tabs.Links.FirstOrDefault()?.Source;

            Buttons = new [] { OkButton, CancelButton };
        }

        public static CarObject Show(CarObject car = null) {
            var dialog = new SelectCarDialog(car ?? CarsManager.Instance.GetDefault());
            dialog.ShowDialog();
            return dialog.IsResultOk ? dialog.SelectedCar : null;
        }

        private bool _loaded;
        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            CarsManager.Instance.WrappersList.ItemPropertyChanged += List_ItemPropertyChanged;
            CarsManager.Instance.WrappersList.WrappedValueChanged += List_WrappedValueChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            CarsManager.Instance.WrappersList.ItemPropertyChanged -= List_ItemPropertyChanged;
            CarsManager.Instance.WrappersList.WrappedValueChanged -= List_WrappedValueChanged;
        }

        void List_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(CarObject.ParentId)) return;

            var car = sender as CarObject;
            if (car == null || SelectedCar == null) return;

            if (car.ParentId == SelectedCar.Id) {
                UpdateTunableVersions();
            }
        }

        void List_WrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            var car = e.NewValue as CarObject;
            if (car == null || SelectedCar == null) return;

            if (car.ParentId == SelectedCar.Id) {
                _previousTunableParent = null;
                UpdateTunableVersions();
            }
        }

        private AcObjectSelectList.AcObjectSelectListViewModel _list;

        private void Tabs_OnFrameNavigated(object sender, NavigationEventArgs e) {
            /* process AcObjectSelectList: unsubscribe from old, check if there is one */
            if (_list != null) {
                _list.PropertyChanged -= List_PropertyChanged;
            }

            _list = (((ModernTab)sender).Frame.Content as AcObjectSelectList)?.Model;
            if (_list == null) return;
            
            _list.SelectedItem = SelectedCar;
            _list.PropertyChanged += List_PropertyChanged;

            /* save uri */
            ValuesStorage.Set(UriKey, e.Source);
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                SelectedCar = _list.SelectedItem as CarObject;
            }
        }

        private RelayCommand _openInShowroomCommand;

        public RelayCommand OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new RelayCommand(o => {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                    !CarOpenInShowroomDialog.Run(SelectedCar, SelectedSkin?.Id)) {
                OpenInShowroomOptionsCommand.Execute(null);
            }
        }, o => SelectedCar != null && SelectedSkin != null));

        private RelayCommand _openInShowroomOptionsCommand;

        public RelayCommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new RelayCommand(o => {
            new CarOpenInShowroomDialog(SelectedCar, SelectedSkin?.Id).ShowDialog();
        }, o => SelectedCar != null && SelectedSkin != null));

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
