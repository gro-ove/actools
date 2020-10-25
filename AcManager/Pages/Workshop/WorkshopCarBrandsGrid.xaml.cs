using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopCarBrandsGrid : IParametrizedUriContent, ILoadableContent {
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
            DataContext = new ViewModel(_categories, _contentType, _searchKeyword);
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private string _contentType;
            private string _searchKeyword;

            private int _layoutPhase;

            public int LayoutPhase {
                get => _layoutPhase;
                set => Apply(value, ref _layoutPhase);
            }

            public List<WorkshopContentCategory> Categories { get; }

            public BetterObservableCollection<WorkshopContentCar> ObjectList { get; } = new BetterObservableCollection<WorkshopContentCar>();

            private WorkshopContentCategory _selectedCategory;

            public WorkshopContentCategory SelectedCategory {
                get => _selectedCategory;
                set => Apply(value, ref _selectedCategory, () => {
                    LoadObjectsAsync().Ignore();
                    UpdateLayoutPhase();
                });
            }

            private WorkshopContentCar _selectedObject;

            public WorkshopContentCar SelectedObject {
                get => _selectedObject;
                set => Apply(value, ref _selectedObject, () => {
                    LoadSelectedObjectAsync().Ignore();
                    UpdateLayoutPhase();
                });
            }

            public ViewModel(List<WorkshopContentCategory> categories, string contentType, string searchKeyword) {
                Categories = categories;
                _contentType = contentType;
                _searchKeyword = searchKeyword;
            }

            private void UpdateLayoutPhase() {
                LayoutPhase = SelectedCategory == null ? 0 : SelectedObject == null ? 1 : 2;
            }

            public async Task LoadObjectsAsync() {
                try {
                    var newList = await new WorkshopClient("http://192.168.1.10:3000")
                            .GetAsync<List<WorkshopContentCar>>($@"/{_contentType}?{_searchKeyword}={Uri.EscapeDataString(SelectedCategory.Name)}");
                    ObjectList.ReplaceEverythingBy_Direct(newList.Repeat(10));
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            public async Task LoadSelectedObjectAsync() {
                try {
                    var selectedObject = SelectedObject;
                    var newObject = await new WorkshopClient("http://192.168.1.10:3000")
                            .GetAsync<WorkshopContentCar>($@"/car/{Uri.EscapeDataString(selectedObject.Id)}");
                    var index = ObjectList.IndexOf(selectedObject);
                    if (index != -1) {
                        ObjectList[index] = newObject;
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        /*private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e) { }

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
        }*/
    }
}