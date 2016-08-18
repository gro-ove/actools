using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class SelectAndSetupCarDialog_Brands : INotifyPropertyChanged {
        public SelectCarDialog MainDialog { get; }

        public class CarBrandInformation : IComparable, IComparable<CarBrandInformation> {
            public string Name { get; set; }

            public string Icon { get; set; }

            internal bool BuiltInIcon;

            public CarBrandInformation(string name) {
                Name = name;
                Icon = GetBrandIcon(name, out BuiltInIcon);
            }

            public override string ToString() {
                return Name;
            }

            public int CompareTo(object obj) {
                return string.Compare(Name, obj.ToString(), StringComparison.InvariantCulture);
            }

            public int CompareTo(CarBrandInformation other) {
                return string.Compare(Name, other.Name, StringComparison.InvariantCulture);
            }
        }

        private static string GetBrandIcon(string brand, out bool builtInIcon) {
            var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, brand + @".png");
            builtInIcon = entry != null && File.Exists(entry.Filename);
            return builtInIcon ? entry.Filename : CarsManager.Instance.LoadedOnly.FirstOrDefault(x => x.Brand == brand)?.BrandBadge;
        }

        public ListCollectionView Brands => _brands;

        private static ListCollectionView _brands;
        private static BetterObservableCollection<CarBrandInformation> _carBrandsInformationList;
        private const string KeyBrandsCache = ".SelectCarDialog.BrandsCache";

        public static void ClearBrandsCache() {
            CacheStorage.Set(KeyBrandsCache, new string[0]);
        }

        private static void UpdateCache() {
            if (CarsManager.Instance.IsLoaded && SettingsHolder.Drive.QuickDriveCacheBrands) {
                CacheStorage.Set(KeyBrandsCache, _carBrandsInformationList.Where(x => x.BuiltInIcon).Select(x => x.Name));
            }
        }

        private class DistinctHelper : IEqualityComparer<CarBrandInformation> {
            public bool Equals(CarBrandInformation x, CarBrandInformation y) => string.Equals(x.Name, y.Name, StringComparison.Ordinal);

            public int GetHashCode(CarBrandInformation obj) => obj.Name.GetHashCode();
        }

        private static void InitializeOnce() {
            if (CarsManager.Instance.IsLoaded) {
                _carBrandsInformationList = new BetterObservableCollection<CarBrandInformation>(SuggestionLists.CarBrandsList.Select(x => new CarBrandInformation(x)));
                UpdateCache();
            } else if (SettingsHolder.Drive.QuickDriveCacheBrands) {
                _carBrandsInformationList = new BetterObservableCollection<CarBrandInformation>(
                        SuggestionLists.CarBrandsList
                                       .Select(x => new CarBrandInformation(x)).Union(from name in CacheStorage.GetStringList(KeyBrandsCache)
                                                                                      select new CarBrandInformation(name))
                                       .Distinct(new DistinctHelper()));
            } else {
                _carBrandsInformationList = new BetterObservableCollection<CarBrandInformation>();
            }

            SuggestionLists.CarBrandsList.CollectionChanged += CarBrandsList_CollectionChanged;
            _brands = (ListCollectionView)CollectionViewSource.GetDefaultView(_carBrandsInformationList);
            _brands.SortDescriptions.Add(new SortDescription());

            CarsManager.Instance.WrappersList.CollectionReady += WrappersList_CollectionReady;
        }

        private static WeakReference<SelectAndSetupCarDialog_Brands> _instance;

        private static void ReplaceBrands(IEnumerable<CarBrandInformation> brands) {
            SelectAndSetupCarDialog_Brands instance;
            _instance.TryGetTarget(out instance);

            var selected = (instance?.BrandsListBox.SelectedItem as CarBrandInformation)?.Name;
            _carBrandsInformationList.ReplaceEverythingBy(brands);

            if (selected != null) {
                instance.BrandsListBox.SelectedItem = _carBrandsInformationList.FirstOrDefault(x => x.Name == selected);
            }
        }

        private static void ReplaceBrands() {
            if (_carBrandsInformationList.Count == SuggestionLists.CarBrandsList.Count &&
                    _carBrandsInformationList.All(x => SuggestionLists.CarBrandsList.Contains(x.Name))) return;
            ReplaceBrands(SuggestionLists.CarBrandsList.Select(x => new CarBrandInformation(x)));
            UpdateCache();
        }

        private static void WrappersList_CollectionReady(object sender, EventArgs e) {
            ReplaceBrands();
        }

        private static void CarBrandsList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    _carBrandsInformationList.AddRange(e.NewItems.OfType<string>()
                                                        .Where(x => _carBrandsInformationList.All(y => !string.Equals(y.Name, x,
                                                                StringComparison.OrdinalIgnoreCase)))
                                                        .Select(x => new CarBrandInformation(x)));
                    break;
                default:
                    ReplaceBrands();
                    break;
            }
        }

        public SelectAndSetupCarDialog_Brands() {
            _instance = new WeakReference<SelectAndSetupCarDialog_Brands>(this);

            MainDialog = SelectCarDialog.Instance;
            MainDialog.PropertyChanged += MainDialog_PropertyChanged;

            InitializeComponent();
            DataContext = this;

            if (_carBrandsInformationList == null) {
                InitializeOnce();
            }

            if (!CarsManager.Instance.IsLoaded) {
                CarsManager.Instance.EnsureLoadedAsync().Forget();
            }

            UpdateSelected();
        }

        void MainDialog_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SelectCarDialog.SelectedCar)) {
                UpdateSelected();
            }
        }

        private void UpdateSelected() {
            if (MainDialog.SelectedCar == null) return;
            var item = _carBrandsInformationList.FirstOrDefault(x => x.Name == MainDialog.SelectedCar.Brand);
            if (item == null) return;
            BrandsListBox.SelectedItem = item;
            BrandsListBox.ScrollIntoView(item);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            BrandsListBox.Focus();
        }

        private void BrandsListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = BrandsListBox.SelectedItem as CarBrandInformation;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(SelectCarDialog.BrandUri(selected.Name), (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var selected = ((FrameworkElement)sender).DataContext as CarBrandInformation;
            if (selected == null) return;
            NavigationCommands.GoToPage.Execute(SelectCarDialog.BrandUri(selected.Name), (IInputElement)sender);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
