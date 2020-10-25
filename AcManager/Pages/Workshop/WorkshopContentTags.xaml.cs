using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopContentTags : IParametrizedUriContent, ILoadableContent {
        private string _contentType;
        private string _tagType;
        private List<WorkshopContentTag> _tags;

        public void OnUri(Uri uri) {
            _contentType = uri.GetQueryParam("Content");
            _tagType = uri.GetQueryParam("Category");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _tags = await new WorkshopClient("http://192.168.1.10:3000")
                    .GetAsync<List<WorkshopContentTag>>($"/{_tagType}", cancellationToken)
                    .ConfigureAwait(false);
        }

        public void Load() {
            LoadAsync(CancellationToken.None).Wait();
        }

        public void Initialize() {
            DataContext = new ViewModel(_tags);
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public List<WorkshopContentTag> Categories { get; }

            public ViewModel(List<WorkshopContentTag> categories) {
                Categories = categories;
            }
        }

        private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e) {
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                if (List.SelectedItem is WorkshopContentTag selected) {
                    List.OpenSubPage(new Uri("/Pages/Workshop/WorkshopContentMain.xaml", UriKind.Relative)
                            .AddQueryParam("Content", _contentType)
                            .AddQueryParam("RawFilter", $@"tags={Uri.EscapeDataString(selected.Name)}"),
                            $"#{selected.Name.TrimStart('#')}", null, @"category");
                }
            }
        }

        private void OnItemMouseDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var element = (FrameworkElement)sender;
            if (element.DataContext is WorkshopContentTag selected) {
                element.OpenSubPage(new Uri("/Pages/Workshop/WorkshopContentMain.xaml", UriKind.Relative)
                        .AddQueryParam("Content", _contentType)
                        .AddQueryParam("RawFilter", $@"tags={Uri.EscapeDataString(selected.Name)}"),
                        $"#{selected.Name.TrimStart('#')}", null, @"category");
            }
        }
    }
}