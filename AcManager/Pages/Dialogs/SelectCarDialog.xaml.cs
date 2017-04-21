using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.Lists;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class SelectCarDialog : INotifyPropertyChanged {
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

        public CarSkinObject SelectedSkin {
            get { return _selectedSkin; }
            set {
                if (Equals(value, _selectedSkin)) return;
                _selectedSkin = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

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

            _openInShowroomCommand?.RaiseCanExecuteChanged();
            _openInCustomShowroomCommand?.RaiseCanExecuteChanged();
            _openInShowroomOptionsCommand?.RaiseCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested(); // TODO

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

        private CommandBase _manageSetupsCommand;

        public ICommand ManageSetupsCommand => _manageSetupsCommand ?? (_manageSetupsCommand = new DelegateCommand(() => {
            if (_selectedCar.Value == null) return;
            CarSetupsListPage.Open(_selectedCar.Value);
        }));

        public static Uri FavouritesUri() {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter=fav+&Title={0}",
                    "Favourites");
        }

        public static Uri RatingUri(double rating) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"rating≥{Filter.Encode(rating.FloorToInt().ToInvariantString())} & rating<{Filter.Encode((rating.FloorToInt() + 1).ToInvariantString())}",
                    PluralizingConverter.PluralizeExt(rating.FloorToInt(), "{0} Star"));
        }

        public static Uri BrandUri(string brand) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&brand:{Filter.Encode(brand)}", brand);
        }

        public static Uri ClassUri(string carClass) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&class:{Filter.Encode(carClass)}", carClass.ToTitle());
        }

        public static Uri YearUri(int? year) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&year:{Filter.Encode((year ?? 0).ToString())}", year != null ? year.ToString() : @"?");
        }

        public static Uri TagUri(string tag) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&tag:{Filter.Encode(tag)}", tag);
        }

        public static Uri CountryUri(string country) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&country:{Filter.Encode(country)}", country);
        }

        public SelectCarDialog(CarObject car, string defaultFilter = null) {
            _selectedCar = new DelayedPropertyWrapper<CarObject>(SelectedCarChanged);

            SelectedCar = car;
            _instance = new WeakReference<SelectCarDialog>(this);

            DataContext = this;
            InputBindings.AddRange(new[] {
                new InputBinding(OpenInShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(OpenInShowroomOptionsCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt | ModifierKeys.Control))
            });
            InitializeComponent();

            CarBlock.BrandArea.PreviewMouseLeftButtonDown += (sender, args) => {
                Tabs.SelectedSource = BrandUri(SelectedCar.Brand);
            };

            CarBlock.ClassArea.PreviewMouseLeftButtonDown += (sender, args) => {
                Tabs.SelectedSource = ClassUri(SelectedCar.CarClass);
            };

            CarBlock.YearArea.PreviewMouseLeftButtonDown += (sender, args) => {
                Tabs.SelectedSource = YearUri(SelectedCar.Year);
            };

            CarBlock.CountryArea.PreviewMouseLeftButtonDown += (sender, args) => {
                Tabs.SelectedSource = CountryUri(SelectedCar.Country);
            };

            Buttons = new [] { OkButton, CancelButton };

            if (defaultFilter != null) {
                Tabs.SavePolicy = SavePolicy.SkipLoading;
                Tabs.SelectedSource = UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={0}", defaultFilter);
            }
        }

        [CanBeNull]
        public static CarObject Show(CarObject car = null) {
            return Show(car, null);
        }

        [CanBeNull]
        public static CarObject Show(string defaultFilter) {
            return Show(null, defaultFilter);
        }

        [CanBeNull]
        public static CarObject Show([CanBeNull] CarObject car, string defaultFilter) {
            var dialog = new SelectCarDialog(car ?? CarsManager.Instance.GetDefault(), defaultFilter);
            dialog.ShowDialog();
            return dialog.IsResultOk ? dialog.SelectedCar : null;
        }

        [CanBeNull]
        public static CarObject Show([CanBeNull] CarObject car, ref CarSkinObject carSkin) {
            var dialog = new SelectCarDialog(car ?? CarsManager.Instance.GetDefault()) {
                SelectedSkin = car?.SkinsActualList.Contains(carSkin) == true ? carSkin : car?.SelectedSkin
            };

            dialog.ShowDialog();

            if (dialog.IsResultOk) {
                carSkin = dialog.SelectedSkin;
                return dialog.SelectedCar;
            }

            return null;
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

        private ISelectedItemPage<AcObjectNew> _list;
        private IChoosingItemControl<AcObjectNew> _choosing;

        private void Tabs_OnFrameNavigated(object sender, NavigationEventArgs e) {
            if (_list != null) {
                _list.PropertyChanged -= List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen -= Choosing_ItemChosen;
            }

            var content = ((ModernTab)sender).Frame.Content;
            _list = content as ISelectedItemPage<AcObjectNew>;
            _choosing = content as IChoosingItemControl<AcObjectNew>;

            if (_list != null) {
                _list.SelectedItem = SelectedCar;
                _list.PropertyChanged += List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen += Choosing_ItemChosen;
            }
        }

        private void Choosing_ItemChosen(object sender, ItemChosenEventArgs<AcObjectNew> e) {
            var c = e.ChosenItem as CarObject;
            if (c != null) {
                SelectedCar = c;
                CloseWithResult(MessageBoxResult.OK);
            }
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                SelectedCar = _list.SelectedItem as CarObject;
            }
        }

        private CommandBase _openInShowroomCommand;

        public ICommand OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new DelegateCommand(() => {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                    !CarOpenInShowroomDialog.Run(SelectedCar, SelectedSkin?.Id)) {
                OpenInShowroomOptionsCommand.Execute(null);
            }
        }, () => SelectedCar != null && SelectedSkin != null));

        private CommandBase _openInCustomShowroomCommand;

        public ICommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ?? (_openInCustomShowroomCommand = new DelegateCommand(() => {
            CustomShowroomWrapper.StartAsync(SelectedCar, SelectedSkin);
        }, () => SelectedCar != null && SelectedSkin != null));

        private CommandBase _openInShowroomOptionsCommand;

        public ICommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new DelegateCommand(() => {
            new CarOpenInShowroomDialog(SelectedCar, SelectedSkin?.Id).ShowDialog();
        }, () => SelectedCar != null && SelectedSkin != null));

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
