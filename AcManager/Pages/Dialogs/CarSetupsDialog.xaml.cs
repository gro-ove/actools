using System;
using System.Threading.Tasks;
using AcManager.Annotations;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class CarSetupsDialog {
        private class CarSetupsDialogModel {
            [NotNull]
            public CarObject SelectedCar { get; }

            public Uri ListUri => UriExtension.Create("/Pages/Lists/CarSetupsListPage.xaml?CarId={0}", SelectedCar.Id);

            public CarSetupsDialogModel([NotNull] CarObject car) {
                SelectedCar = car;
            }
        }

        private CarSetupsDialogModel Model => (CarSetupsDialogModel)DataContext;

        public CarSetupsDialog([NotNull] CarObject car) {
            if (car == null) throw new ArgumentNullException(nameof(car));

            DataContext = new CarSetupsDialogModel(car);

            DefaultContentSource = Model.ListUri;
            MenuLinkGroups.Add(new LinkGroupFilterable {
                DisplayName = "setups",
                Source = Model.ListUri
            });

            InitializeComponent();
        }

        private void CarSetupsDialog_OnInitialized(object sender, EventArgs e) {
            if (Model?.SelectedCar == null) return;
            Model.SelectedCar.AcObjectOutdated += SelectedCar_AcObjectOutdated;
        }

        private void CarSetupsDialog_OnClosed(object sender, EventArgs e) {
            if (Model?.SelectedCar == null) return;
            Model.SelectedCar.AcObjectOutdated -= SelectedCar_AcObjectOutdated;
        }

        private async void SelectedCar_AcObjectOutdated(object sender, EventArgs e) {
            Hide();

            await Task.Delay(10);
            Close();
        }
    }
}
