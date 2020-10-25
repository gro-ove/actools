using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopContentCategories : IParametrizedUriContent, ILoadableContent {
        private string _contentType;
        private string _categoryType;
        private string _searchKeyword;
        private List<WorkshopContentCategory> _categories;

        public void OnUri(Uri uri) {
            _contentType = uri.GetQueryParam("Content");
            _categoryType = uri.GetQueryParam("Category");
            _searchKeyword = uri.GetQueryParam("SearchBy");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _categories = (await new WorkshopClient("http://192.168.1.10:3000")
                    .GetAsync<List<WorkshopContentCategory>>($"/{_categoryType}", cancellationToken)
                    .ConfigureAwait(false)).Repeat(10).ToList();
        }

        public void Load() {
            LoadAsync(CancellationToken.None).Wait();
        }

        public void Initialize() {
            DataContext = new ViewModel(_categories);
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public List<WorkshopContentCategory> Categories { get; }

            public ViewModel(List<WorkshopContentCategory> categories) {
                Categories = categories;
            }
        }

        private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e) { }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                if (List.SelectedItem is WorkshopContentCategory selected) {
                    List.OpenSubPage(new Uri("/Pages/Workshop/WorkshopContentMain.xaml", UriKind.Relative)
                            .AddQueryParam("Content", _contentType)
                            .AddQueryParam("RawFilter", $@"{_searchKeyword}={Uri.EscapeDataString(selected.Name)}"),
                            selected.Name, selected.Icon, @"category");
                }
            }
        }

        private void OnItemMouseDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var element = (FrameworkElement)sender;
            if (element.DataContext is WorkshopContentCategory selected) {
                element.OpenSubPage(new Uri("/Pages/Workshop/WorkshopContentMain.xaml", UriKind.Relative)
                        .AddQueryParam("Content", _contentType)
                        .AddQueryParam("RawFilter", $@"{_searchKeyword}={Uri.EscapeDataString(selected.Name)}"),
                        selected.Name, selected.Icon, @"category");
            }
        }
    }
}