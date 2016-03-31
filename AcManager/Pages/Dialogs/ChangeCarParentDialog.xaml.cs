using System.Collections;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    public partial class ChangeCarParentDialog : IComparer {
        public CarObject Car { get; }

        public ListCollectionView CarsListView { get; }

        private string _filter;

        public string Filter {
            get { return _filter; }
            private set {
                if (value == _filter) return;
                _filter = value;
                UpdateFilter();
            }
        }

        public ChangeCarParentDialog(CarObject car) {
            InitializeComponent();
            DataContext = this;

            Buttons = new[] {
                OkButton, 
                CreateExtraDialogButton("Make Independent", () => {
                    Car.ParentId = null;
                    Close();
                }),
                CancelButton
            };

            Car = car;
            Filter = car.Brand == null ? "" : "brand:" + car.Brand;

            CarsListView = new ListCollectionView(CarsManager.Instance.LoadedOnly.Where(x => x.ParentId == null && x.Id != Car.Id).ToList()) {
                CustomSort = this
            };

            UpdateFilter();
            if (car.Parent == null) {
                CarsListView.MoveCurrentToPosition(0);
            } else {
                CarsListView.MoveCurrentTo(car.Parent);
            }

            Closing += CarParentEditor_Closing;
        }

        void CarParentEditor_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!IsResultOk) return;

            var current = CarsListView.CurrentItem as CarObject;
            if (current != null) {
                if (Car.Children.Any()) {
                    if (ModernDialog.ShowMessage(@"Children will be moved to the new parent!", @"Warning", MessageBoxButton.OKCancel) !=
                        MessageBoxResult.OK) {
                        return;
                    }
                }

                if (!File.Exists(Car.UpgradeIcon)) {
                    var dialog = new UpgradeIconEditor(Car);
                    dialog.ShowDialog();
                    if (!dialog.IsResultOk) return;
                }

                foreach (var child in Car.Children) {
                    child.ParentId = current.Id;
                }

                Car.ParentId = current.Id;
            } else {
                Car.ParentId = null;
            }
        }

        private void UpdateFilter() {
            var filter = Filter;
            var listView = CarsListView;

            if (listView == null) return;

            using (listView.DeferRefresh()) {
                if (string.IsNullOrEmpty(filter)) {
                    listView.Filter = null;
                } else {
                    var temp = StringBasedFilter.Filter.Create(CarObjectTester.Instance, Filter);
                    listView.Filter = x => x is CarObject && temp.Test((CarObject)x);
                }
            }
        }

        public int Compare(object x, object y) {
            return ((CarObject)x).CompareTo((CarObject)y);
        }
    }
}
