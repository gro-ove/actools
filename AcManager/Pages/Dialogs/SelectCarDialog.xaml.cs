using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AcManager.Controls;
using JetBrains.Annotations;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.CustomShowroom;
using AcManager.Pages.Lists;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class SelectCarDialog : IInvokingNotifyPropertyChanged {
        /*private static WeakReference<SelectCarDialog> _instance;
        public static SelectCarDialog Instance => _instance == null ? null : _instance.TryGetTarget(out var result) ? result : null;*/

        private CarSkinObject _selectedSkin;

        [CanBeNull]
        public CarSkinObject SelectedSkin {
            get => _selectedSkin;
            set {
                if (Equals(value, _selectedSkin)) return;
                _selectedSkin = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private readonly DelayedPropertyWrapper<CarObject> _selectedCar;

        [CanBeNull]
        public CarObject SelectedCar {
            get => _selectedCar.Value;
            set => _selectedCar.Value = value ?? SelectedCar;
        }

        private CarObject _selectedTunableVersion;

        [CanBeNull]
        public CarObject SelectedTunableVersion {
            get => _selectedTunableVersion;
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

            var parent = SelectedCar?.ParentId == null ? SelectedCar : SelectedCar.Parent;
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
            get => _hasChildren;
            set => this.Apply(value, ref _hasChildren);
        }

        public BetterObservableCollection<CarObject> TunableVersions { get; } = new BetterObservableCollection<CarObject>();

        private CommandBase _manageSetupsCommand;

        public ICommand ManageSetupsCommand => _manageSetupsCommand ?? (_manageSetupsCommand = new DelegateCommand(() => {
            if (_selectedCar.Value == null) return;
            CarSetupsListPage.Open(_selectedCar.Value, CarSetupsRemoteSource.None, true);
        }));

        public static Uri FavouritesUri() {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter=fav+&Title={0}",
                    AppStrings.Online_Sorting_Favourites);
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
                    $"enabled+&tag:{Filter.Encode(tag)}", "#" + tag);
        }

        public static Uri CountryUri(string country) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&country:{Filter.Encode(country)}", country);
        }

        public SelectCarDialog([CanBeNull] CarObject car, string defaultFilter = null) {
            _selectedCar = new DelayedPropertyWrapper<CarObject>(SelectedCarChanged);

            SelectedCar = car;
            // _instance = new WeakReference<SelectCarDialog>(this);

            DataContext = this;
            InputBindings.AddRange(new[] {
                new InputBinding(ToggleFavouriteCommand, new KeyGesture(Key.B, ModifierKeys.Control)),
                new InputBinding(OpenInShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(OpenInShowroomOptionsCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt | ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => OnScrollToSelectedButtonClick(null, null)),
                        new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift))
            });
            InitializeComponent();
            Buttons = new Control[0];

            if (defaultFilter != null) {
                Tabs.SavePolicy = SavePolicy.SkipLoadingFlexible;
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
        public static CarObject Show([CanBeNull] CarObject car, [CanBeNull] ref CarSkinObject carSkin) {
            var dialog = new SelectCarDialog(car ?? CarsManager.Instance.GetDefault()) {
                SelectedSkin = car?.EnabledSkinsListView.Contains(carSkin) == true ? carSkin : car?.SelectedSkin
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
            if (_loaded || SelectedCar == null) return;
            _loaded = true;
            CarsManager.Instance.WrappersList.ItemPropertyChanged += OnListItemPropertyChanged;
            CarsManager.Instance.WrappersList.WrappedValueChanged += OnListWrappedValueChanged;

            if (CarBlock.BrandArea != null) {
                CarBlock.BrandArea.PreviewMouseLeftButtonUp += (s, args) => Tabs.SelectedSource = BrandUri(SelectedCar.Brand);
            }

            if (CarBlock.ClassArea != null) {
                CarBlock.ClassArea.PreviewMouseLeftButtonUp += (s, args) => Tabs.SelectedSource = ClassUri(SelectedCar.CarClass);
            }

            if (CarBlock.YearArea != null) {
                CarBlock.YearArea.PreviewMouseLeftButtonUp += (s, args) => Tabs.SelectedSource = YearUri(SelectedCar.Year);
            }

            if (CarBlock.CountryArea != null) {
                CarBlock.CountryArea.PreviewMouseLeftButtonUp += (s, args) => Tabs.SelectedSource = CountryUri(SelectedCar.Country);
            }

            if (CarBlock.TagsList != null) {
                CarBlock.TagsList.PreviewMouseLeftButtonUp += (s, args) => {
                    if (CarBlock.TagsList.FindVisualChild<ItemsControl>().GetMouseItem() is string mouseTag) {
                        Tabs.SelectedSource = TagUri(mouseTag);
                    }
                };
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            CarsManager.Instance.WrappersList.ItemPropertyChanged -= OnListItemPropertyChanged;
            CarsManager.Instance.WrappersList.WrappedValueChanged -= OnListWrappedValueChanged;
        }

        private void OnListItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(CarObject.ParentId) && sender is CarObject car && SelectedCar != null) {
                if (car.ParentId == SelectedCar.Id) {
                    UpdateTunableVersions();
                }
            }
        }

        private void OnListWrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            if (e.NewValue is CarObject car && SelectedCar != null && car.ParentId == SelectedCar.Id) {
                _previousTunableParent = null;
                UpdateTunableVersions();
            }
        }

        private ISelectedItemPage<AcObjectNew> _list;
        private IChoosingItemControl<AcObjectNew> _choosing;

        private void OnTabsNavigated(object sender, NavigationEventArgs e) {
            if (_list != null) {
                _list.PropertyChanged -= OnListPropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen -= OnChoosingItemChosen;
            }

            var content = ((ModernTab)sender).Frame.Content;
            _list = content as ISelectedItemPage<AcObjectNew>;
            _choosing = content as IChoosingItemControl<AcObjectNew>;

            if (_list != null) {
                _list.SelectedItem = SelectedCar;
                _list.PropertyChanged += OnListPropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen += OnChoosingItemChosen;
            }

            if (content is AcObjectSelectList) {
                UpdateHint();
            }
        }

        private async void UpdateHint() {
            await Task.Delay(1);
            if (Tabs.ActualWidth <= AcObjectListBox.AutoThumbnailModeThresholdValue) {
                FancyHints.CarDialogThumbinalMode.Trigger();
            }
        }

        private void OnChoosingItemChosen(object sender, ItemChosenEventArgs<AcObjectNew> e) {
            if (e.ChosenItem is CarObject c) {
                SelectedCar = c;
                CloseWithResult(MessageBoxResult.OK);
            }
        }

        private void OnListPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                SelectedCar = _list.SelectedItem as CarObject;
            }
        }

        private DelegateCommand _toggleFavouriteCommand;

        public DelegateCommand ToggleFavouriteCommand => _toggleFavouriteCommand ?? (_toggleFavouriteCommand = new DelegateCommand(() => {
            if (SelectedCar == null) return;
            SelectedCar.IsFavourite = !SelectedCar.IsFavourite;
        }, () => SelectedCar != null));

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

        private void OnSeparatorDrag(object sender, DragDeltaEventArgs e) {
            if (Tabs.ActualWidth > AcObjectListBox.AutoThumbnailModeThresholdValue) {
                FancyHints.CarDialogThumbinalMode.MaskAsUnnecessary();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void IInvokingNotifyPropertyChanged.OnPropertyChanged(string propertyName) {
            OnPropertyChanged(propertyName);
        }

        private void OnScrollToSelectedButtonClick(object sender, RoutedEventArgs e) {
            var list = (Tabs.Frame.Content as FrameworkElement)?.FindVisualChild<ListBox>();
            list?.ScrollIntoView(list.SelectedItem);
        }
    }
}
