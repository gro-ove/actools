using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Pages.Dialogs {
    public partial class SelectAndSetupCarDialog_Categories {
        private SelectAndSetupCarDialog_CategoriesViewModel Model => (SelectAndSetupCarDialog_CategoriesViewModel) DataContext;

        public SelectAndSetupCarDialog_Categories() {
            DataContext = new SelectAndSetupCarDialog_CategoriesViewModel();
            InitializeComponent();
        }

        private void CategoriesListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = CategoriesListBox.SelectedItem as CarCategory;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var selected = ((FrameworkElement)sender).DataContext as CarCategory;
            e.Handled = true;
            if (selected == null) return;
            NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
        }

        public class CarCategory {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("filter")]
            public string Filter { get; set; }

            [JsonProperty("icon")]
            public string Icon { get; set; }

            public Uri PageAddress => UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}", 
                    $"enabled+&({Filter})", Name);
        }

        public class SelectAndSetupCarDialog_CategoriesViewModel : NotifyPropertyChanged {
            public SelectAndSetupCarDialog_CategoriesViewModel() {
                FilesStorage.Instance.Watcher(ContentCategory.CarCategories).Update += SelectTrackDialog_CategoriesViewModel_LibraryUpdate;
                Categories = new BetterObservableCollection<CarCategory>(ReloadCategories());
            }

            private IEnumerable<CarCategory> ReloadCategories() {
                var text = FilesStorage.Instance.LoadContentFile(ContentCategory.CarCategories, "List.json");
                return text == null ? new CarCategory[]{} : JsonConvert.DeserializeObject<CarCategory[]>(text).Select(x => {
                    x.Icon = FilesStorage.Instance.GetContentFile(ContentCategory.CarCategories, x.Icon ?? (x.Name + ".png")).Filename;  
                    return x;
                });
            }

            private void SelectTrackDialog_CategoriesViewModel_LibraryUpdate(object sender, EventArgs e) {
                Categories.ReplaceEverythingBy(ReloadCategories());
            }

            private BetterObservableCollection<CarCategory> _categories;

            public BetterObservableCollection<CarCategory> Categories {
                get { return _categories; }
                set {
                    if (Equals(value, _categories)) return;
                    _categories = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
