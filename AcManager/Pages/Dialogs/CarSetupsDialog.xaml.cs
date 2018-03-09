using System;
using System.Threading.Tasks;
using AcManager.Controls.Helpers;
using AcManager.Pages.Lists;
using AcManager.Tools.Managers;
using JetBrains.Annotations;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class CarSetupsDialog {
        private class ViewModel {
            [NotNull]
            public CarObject SelectedCar { get; }

            public Uri ListUri => UriExtension.Create("/Pages/Lists/CarSetupsListPage.xaml?CarId={0}", SelectedCar.Id);

            public ViewModel([NotNull] CarObject car) {
                SelectedCar = car;
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        private CarSetupsDialog([NotNull] CarObject car, CarSetupsRemoteSource forceRemoteSource = CarSetupsRemoteSource.None) {
            if (car == null) throw new ArgumentNullException(nameof(car));

            DataContext = new ViewModel(car);
            DefaultContentSource = Model.ListUri;

            var linkGroup = new LinkGroupFilterable {
                DisplayName = AppStrings.Main_Setups,
                Source = Model.ListUri,
                AddAllLink = true,
                FilterHint = FilterHints.CarSetups
            };

            foreach (var link in CarSetupsListPage.GetRemoteLinks(car.Id)) {
                linkGroup.FixedLinks.Add(link);
            }

            if (forceRemoteSource != CarSetupsRemoteSource.None) {
                ValuesStorage.Set("CarSetupsDialog_link", CarSetupsListPage.GetRemoteSourceUri(car.Id, forceRemoteSource));
            }

            MenuLinkGroups.Add(linkGroup);
            InitializeComponent();
        }

        public static void Show([NotNull] CarObject car, CarSetupsRemoteSource forceRemoteSource = CarSetupsRemoteSource.None) {
            new CarSetupsDialog(car, forceRemoteSource) {
                ShowInTaskbar = false
            }.ShowDialogAsync().Forget();
        }

        private void OnInitialized(object sender, EventArgs e) {
            if (Model?.SelectedCar == null) return;
            Model.SelectedCar.AcObjectOutdated += SelectedCar_AcObjectOutdated;
        }

        private void OnClosed(object sender, EventArgs e) {
            if (Model?.SelectedCar == null) return;
            Model.SelectedCar.AcObjectOutdated -= SelectedCar_AcObjectOutdated;
        }

        private async void SelectedCar_AcObjectOutdated(object sender, EventArgs e) {
            Hide();
            await Task.Yield();
            Close();
        }
    }
}
