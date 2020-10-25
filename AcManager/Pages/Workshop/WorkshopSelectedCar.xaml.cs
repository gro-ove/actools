using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopSelectedCar : IParametrizedUriContent, ILoadableContent {
        private string _id;
        private WorkshopContentCar _car;

        private ViewModel Model => (ViewModel)DataContext;

        public void OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            _car = await new WorkshopClient("http://192.168.1.10:3000")
                    .GetAsync<WorkshopContentCar>($"/cars/{_id}", cancellationToken)
                    .ConfigureAwait(false);
            if (_car == null) {
                throw new Exception("Car is missing");
            }
        }

        public void Load() {
            LoadAsync(CancellationToken.None).Wait();
        }

        public void Initialize() {
            DataContext = new ViewModel(_car);
            InitializeComponent();
            SkinsList.SelectedItem = Model.SelectedObject.Skins?.First();
        }

        public class ViewModel : NotifyPropertyChanged {
            public WorkshopContentCar SelectedObject { get; }

            public ViewModel(WorkshopContentCar selectedObject) {
                SelectedObject = selectedObject;
            }
        }

        private void OnUpgradeIconClick(object sender, MouseButtonEventArgs e) {
        }

        private void OnPreviewClick(object sender, MouseButtonEventArgs e) {
        }

        private void OnPreviewRightClick(object sender, MouseButtonEventArgs e) {
        }
    }
}