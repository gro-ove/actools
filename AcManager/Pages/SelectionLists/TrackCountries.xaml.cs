using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using JetBrains.Annotations;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Pages.SelectionLists {
    public partial class TrackCountries : ISelectedItemPage<AcObjectNew> {
        private AcObjectNew _selectedItem;

        public AcObjectNew SelectedItem {
            get { return _selectedItem; }
            set {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;

                UpdateSelected(value as TrackObjectBase);
            }
        }

        public ListCollectionView Countries => _countries;

        private static ListCollectionView _countries;
        private static BetterObservableCollection<SelectCountry> _countriesInformationList;

        private static void InitializeOnce() {
            _countriesInformationList = new BetterObservableCollection<SelectCountry>(
                SuggestionLists.CountriesList.Select(x => new SelectCountry(x))
            );

            SuggestionLists.CountriesList.CollectionChanged += CountriesList_CollectionChanged;
            _countries = (ListCollectionView)CollectionViewSource.GetDefaultView(_countriesInformationList);
            _countries.SortDescriptions.Add(new SortDescription());
        }

        private static Uri GetPageAddress(SelectCountry category) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}",
                $"enabled+&country:{Filter.Encode(category.DisplayName)}", category.DisplayName);
        }

        private static void CountriesList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null) {
                        _countriesInformationList.AddRange(e.NewItems.Cast<string>().Select(x => new SelectCountry(x)));
                        return;
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    return;
            }

            _countriesInformationList.ReplaceEverythingBy(
                SuggestionLists.CountriesList.Select(x => new SelectCountry(x))
            );
        }

        public TrackCountries() {
            InitializeComponent();
            DataContext = this;

            if (_countriesInformationList == null) {
                InitializeOnce();
            }

            if (!TracksManager.Instance.IsLoaded) {
                TracksManager.Instance.EnsureLoadedAsync().Forget();
            }
        }

        private void UpdateSelected(TrackObjectBase track) {
            var item = _countriesInformationList.FirstOrDefault(x => x.DisplayName == track.Country);
            if (item == null) return;
            CountriesListBox.SelectedItem = item;
            CountriesListBox.ScrollIntoView(item);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            CountriesListBox.Focus();
        }

        private void CountriesListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = CountriesListBox.SelectedItem as SelectCountry;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var selected = ((FrameworkElement)sender).DataContext as SelectCountry;
            if (selected == null) return;
            NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
