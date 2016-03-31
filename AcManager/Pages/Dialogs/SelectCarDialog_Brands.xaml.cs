using System;
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
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public partial class SelectAndSetupCarDialog_Brands : INotifyPropertyChanged {
        public SelectCarDialog MainDialog { get; }

        public class CarBrandInformation : IComparable, IComparable<CarBrandInformation> {
            public string Name { get; set; }

            public string Icon { get; set; }

            public Uri PageAddress => UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&brand:{Name}", Name);

            public CarBrandInformation(string name) {
                Name = name;
                Icon = GetBrandIcon(name);
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

        private static string GetBrandIcon(string brand) {
            var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, brand + ".png");
            if (entry != null && File.Exists(entry.Filename)) return entry.Filename;

            var fromList = CarsManager.Instance.LoadedOnly.FirstOrDefault(x => x.Brand == brand);
            return fromList?.BrandBadge;
        }

        public ListCollectionView Brands => _brands;

        private static ListCollectionView _brands;
        private static BetterObservableCollection<CarBrandInformation> _carBrandsInformationList;

        private static void InitializeOnce() {
            _carBrandsInformationList = new BetterObservableCollection<CarBrandInformation>(
                SuggestionLists.CarBrandsList.Select(x => new CarBrandInformation(x))
            );

            SuggestionLists.CarBrandsList.CollectionChanged += CarBrandsList_CollectionChanged;

            _brands = (ListCollectionView)CollectionViewSource.GetDefaultView(_carBrandsInformationList);
            _brands.SortDescriptions.Add(new SortDescription());
        }

        private static void CarBrandsList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    _carBrandsInformationList.AddRange(e.NewItems.Cast<string>().Select(x => new CarBrandInformation(x)));
                    break;

                default:
                    _carBrandsInformationList.ReplaceEverythingBy(
                        SuggestionLists.CarBrandsList.Select(x => new CarBrandInformation(x))
                    );
                    break;
            }
        }

        public SelectAndSetupCarDialog_Brands() {
            MainDialog = SelectCarDialog.Instance;
            MainDialog.PropertyChanged += MainDialog_PropertyChanged;

            InitializeComponent();
            DataContext = this;

            CarsManager.Instance.EnsureLoadedAsync().Forget();
            if (_carBrandsInformationList == null) {
                InitializeOnce();
            }

            UpdateSelected();
        }

        void MainDialog_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "SelectedCar") {
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

        private void SelectAndSetupCarDialog_Brands_OnLoaded(object sender, RoutedEventArgs e) {
            BrandsListBox.Focus();
        }

        private void BrandsListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = BrandsListBox.SelectedItem as CarBrandInformation;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var selected = ((FrameworkElement)sender).DataContext as CarBrandInformation;
            if (selected == null) return;
            e.Handled = true;
            NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
