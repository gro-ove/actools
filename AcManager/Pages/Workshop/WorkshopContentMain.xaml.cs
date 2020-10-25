using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopContentCars : IParametrizedUriContent, ILoadableContent {
        private string _content;
        private string _filter;
        private string _rawFilter;
        private List<WorkshopContentCar> _list;

        private ViewModel Model => (ViewModel)DataContext;

        public void OnUri(Uri uri) {
            _content = uri.GetQueryParam("Content");
            _filter = uri.GetQueryParam("Filter");
            _rawFilter = uri.GetQueryParam("RawFilter");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _list = await new WorkshopClient("http://192.168.1.10:3000")
                    .GetAsync<List<WorkshopContentCar>>(_rawFilter != null ? $@"/{_content}?{_rawFilter}"
                            : _filter != null ? $@"/{_content}?filter={Uri.EscapeDataString(_filter)}"
                                    : $@"/{_content}", cancellationToken)
                    .ConfigureAwait(false);
        }

        public void Load() {
            LoadAsync(CancellationToken.None).Wait();
        }

        public void Initialize() {
            DataContext = new ViewModel(_list);
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public List<WorkshopContentCar> List { get; }

            public ViewModel(List<WorkshopContentCar> list) {
                List = list;
            }
        }

        private void OnItemMouseDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            Logging.Debug("selected=" + List.SelectedItem);
            if (List.SelectedItem is WorkshopContentCar selected) {
                List.OpenSubPage(new Uri("/Pages/Workshop/WorkshopSelectedCar.xaml", UriKind.Relative)
                        .AddQueryParam("Id", selected.Id),
                        selected.Name, selected.BrandBadge.Value, @"selected");
            }
        }
    }
}