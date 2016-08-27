using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Pages.SelectionLists;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class SelectTrackDialog_Countries : INotifyPropertyChanged {
        public SelectTrackDialog.ViewModel MainDialog { get; }

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
                    _countriesInformationList.AddRange(e.NewItems.Cast<string>().Select(x => new SelectCountry(x)));
                    break;

                default:
                    _countriesInformationList.ReplaceEverythingBy(
                        SuggestionLists.CountriesList.Select(x => new SelectCountry(x))
                    );
                    break;
            }
        }

        public SelectTrackDialog_Countries() {
            MainDialog = SelectTrackDialog.Instance.Model;
            MainDialog.PropertyChanged += MainDialog_PropertyChanged;

            InitializeComponent();
            DataContext = this;

            TracksManager.Instance.EnsureLoadedAsync().Forget();
            if (_countriesInformationList == null) {
                InitializeOnce();
            }

            UpdateSelected();
        }

        void MainDialog_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SelectTrackDialog.ViewModel.SelectedTrackConfiguration)) {
                UpdateSelected();
            }
        }

        private void UpdateSelected() {
            if (MainDialog.SelectedTrack == null) return;
            var item = _countriesInformationList.FirstOrDefault(x => x.DisplayName == MainDialog.SelectedTrack.Country);
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
