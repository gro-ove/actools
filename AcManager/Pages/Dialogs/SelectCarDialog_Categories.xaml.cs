using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class SelectAndSetupCarDialog_Categories {
        public ViewModel Model => (ViewModel) DataContext;

        public SelectAndSetupCarDialog_Categories() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private static Uri GetPageAddress(SelectCategory category) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                $"enabled+&({category.Filter})", category.DisplayName);
        }

        private void CategoriesListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = CategoriesListBox.SelectedItem as SelectCategory;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var selected = ((FrameworkElement)sender).DataContext as SelectCategory;
            e.Handled = true;
            if (selected == null) return;
            NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {
                FilesStorage.Instance.Watcher(ContentCategory.CarCategories).Update += SelectTrackDialog_CategoriesViewModel_LibraryUpdate;
                Categories = new BetterObservableCollection<SelectCategory>(ReloadCategories());
            }

            private IEnumerable<SelectCategory> ReloadCategories() {
                return SelectCategory.LoadCategories(ContentCategory.CarCategories);
            }

            private void SelectTrackDialog_CategoriesViewModel_LibraryUpdate(object sender, EventArgs e) {
                Categories.ReplaceEverythingBy(ReloadCategories());
            }

            public BetterObservableCollection<SelectCategory> Categories { get; }
        }
    }
}
