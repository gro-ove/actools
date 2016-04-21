using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class SelectTrackDialog_Countries : INotifyPropertyChanged {
        public SelectTrackDialog.SelectTrackDialogViewModel MainDialog { get; }

        public class CountryInformation : IComparable, IComparable<CountryInformation> {
            [NotNull]
            public string Name { get; }

            [CanBeNull]
            public string CountryId { get; }

            public Uri PageAddress => UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}",
                    $"enabled+&country:{Filter.Encode(Name)}", Name);

            public CountryInformation([NotNull] string name) {
                if (name == null) throw new ArgumentNullException(nameof(name));

                Name = name;
                CountryId = DataProvider.Instance.CountryToIds.GetValueOrDefault(AcStringValues.CountryFromTag(name) ?? "");
            }

            public override string ToString() => Name;

            int IComparable.CompareTo(object obj) => string.Compare(Name, obj.ToString(), StringComparison.InvariantCulture);

            int IComparable<CountryInformation>.CompareTo(CountryInformation other) => string.Compare(Name, other.Name, StringComparison.InvariantCulture);
        }

        public ListCollectionView Countries => _countries;

        private static ListCollectionView _countries;
        private static BetterObservableCollection<CountryInformation> _countriesInformationList;

        private static void InitializeOnce() {
            _countriesInformationList = new BetterObservableCollection<CountryInformation>(
                SuggestionLists.CountriesList.Select(x => new CountryInformation(x))
            );

            SuggestionLists.CountriesList.CollectionChanged += CountriesList_CollectionChanged;
            _countries = (ListCollectionView)CollectionViewSource.GetDefaultView(_countriesInformationList);
            _countries.SortDescriptions.Add(new SortDescription());
        }

        private static void CountriesList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    _countriesInformationList.AddRange(e.NewItems.Cast<string>().Select(x => new CountryInformation(x)));
                    break;

                default:
                    _countriesInformationList.ReplaceEverythingBy(
                        SuggestionLists.CountriesList.Select(x => new CountryInformation(x))
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
            if (e.PropertyName == nameof(SelectTrackDialog.SelectTrackDialogViewModel.SelectedTrackConfiguration)) {
                UpdateSelected();
            }
        }

        private void UpdateSelected() {
            if (MainDialog.SelectedTrack == null) return;
            var item = _countriesInformationList.FirstOrDefault(x => x.Name == MainDialog.SelectedTrack.Country);
            if (item == null) return;
            CountriesListBox.SelectedItem = item;
            CountriesListBox.ScrollIntoView(item);
        }

        private void SelectTrackDialog_Countries_OnLoaded(object sender, RoutedEventArgs e) {
            CountriesListBox.Focus();
        }

        private void CountriesListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = CountriesListBox.SelectedItem as CountryInformation;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var selected = ((FrameworkElement)sender).DataContext as CountryInformation;
            if (selected == null) return;
            NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
