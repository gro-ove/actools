using System;
using System.Windows.Controls;
using AcManager.Annotations;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class CarSkinsDialog {
        private class CarSkinsDialogModel {
            [NotNull]
            public CarObject SelectedCar { get; }

            public Uri ListUri => UriExtension.Create("/Pages/Lists/CarSkinsListPage.xaml?CarId={0}", SelectedCar.Id);

            public CarSkinsDialogModel([NotNull] CarObject car) {
                SelectedCar = car;
            }
        }

        private CarSkinsDialogModel Model => (CarSkinsDialogModel) DataContext;

        public CarSkinsDialog([NotNull] CarObject car) {
            DataContext = new CarSkinsDialogModel(car);

            DefaultContentSource = Model.ListUri;
            MenuLinkGroups.Add(new LinkGroupFilterable {
                Source = Model.ListUri
            });

            InitializeComponent();
        }
    }
}
