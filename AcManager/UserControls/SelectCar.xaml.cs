using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;

namespace AcManager.UserControls {
    public partial class SelectCar : IChoosingItemControl<CarObject> {
        public SelectCar() {
            InitializeComponent();
        }

        public static readonly DependencyProperty SelectedCarProperty = DependencyProperty.Register(nameof(SelectedCar), typeof(CarObject),
                typeof(SelectCar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedCarChanged));

        public CarObject SelectedCar {
            get { return (CarObject)GetValue(SelectedCarProperty); }
            set { SetValue(SelectedCarProperty, value); }
        }

        private static void OnSelectedCarChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((SelectCar)o).OnSelectedCarChanged((CarObject)e.NewValue);
        }

        private void OnSelectedCarChanged(CarObject newValue) {
            Logging.Debug(newValue);
            Logging.Debug("list: " + _list);
            if (_list != null) {
                _list.SelectedItem = newValue;
            }
        }

        private ISelectedItemPage<AcObjectNew> _list;
        private IChoosingItemControl<AcObjectNew> _choosing;

        private void OnFrameNavigated(object sender, NavigationEventArgs e) {
            if (_list != null) {
                _list.PropertyChanged -= List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen -= Choosing_ItemChosen;
            }

            var content = ((ModernTab)sender).Frame.Content;
            _list = content as ISelectedItemPage<AcObjectNew>;
            _choosing = content as IChoosingItemControl<AcObjectNew>;
            
            if (_list != null) {
                _list.SelectedItem = SelectedCar;
                _list.PropertyChanged += List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen += Choosing_ItemChosen;
            }
        }

        public IEnumerable<CarObject> GetSelectedCars() {
            var items = _list as ISelectedItemsPage<AcObjectNew>;
            return items?.GetSelectedItems().OfType<CarObject>() ?? new[] { SelectedCar };
        }

        public event EventHandler<ItemChosenEventArgs<CarObject>> ItemChosen;

        private void Choosing_ItemChosen(object sender, ItemChosenEventArgs<AcObjectNew> e) {
            var c = e.ChosenItem as CarObject;
            if (c != null) {
                ItemChosen?.Invoke(this, new ItemChosenEventArgs<CarObject>(c));
            }
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                SelectedCar = _list.SelectedItem as CarObject;
            }
        }
    }
}
