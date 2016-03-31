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
    /// <summary>
    /// Interaction logic for SelectTrackDialog_Categories.xaml
    /// </summary>
    public partial class SelectTrackDialog_Categories {
        private SelectTrackDialog_CategoriesViewModel Model => (SelectTrackDialog_CategoriesViewModel) DataContext;

        public SelectTrackDialog_Categories() {
            DataContext = new SelectTrackDialog_CategoriesViewModel();
            InitializeComponent();
        }

        private void CategoriesListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = CategoriesListBox.SelectedItem as TrackCategory;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var selected = ((FrameworkElement)sender).DataContext as TrackCategory;
            e.Handled = true;
            if (selected == null) return;
            NavigationCommands.GoToPage.Execute(selected.PageAddress, (IInputElement)sender);
        }

        public class TrackCategory {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("filter")]
            public string Filter { get; set; }

            [JsonProperty("icon")]
            public string Icon { get; set; }

            public Uri PageAddress => UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}", 
                    $"enabled+&({Filter})", Name);
        }

        public class SelectTrackDialog_CategoriesViewModel : NotifyPropertyChanged {
            public SelectTrackDialog_CategoriesViewModel() {
                FilesStorage.Instance.Watcher(ContentCategory.TrackCategories).Update += SelectTrackDialog_CategoriesViewModel_LibraryUpdate;
                Categories = new BetterObservableCollection<TrackCategory>(ReloadCategories());
            }

            private IEnumerable<TrackCategory> ReloadCategories() {
                var text = FilesStorage.Instance.LoadContentFile(ContentCategory.TrackCategories, "List.json");
                return text == null ? new TrackCategory []{} : JsonConvert.DeserializeObject<TrackCategory[]>(text).Select(x => {
                    x.Icon = FilesStorage.Instance.GetContentFile(ContentCategory.TrackCategories, x.Icon ?? (x.Name + ".png")).Filename;  
                    return x;
                });
            }

            private void SelectTrackDialog_CategoriesViewModel_LibraryUpdate(object sender, EventArgs e) {
                Categories.ReplaceEverythingBy(ReloadCategories());
            }

            private BetterObservableCollection<TrackCategory> _categories;

            public BetterObservableCollection<TrackCategory> Categories {
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
