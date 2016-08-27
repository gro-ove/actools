using System.ComponentModel;
using System.Windows;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;

namespace AcManager.UserControls {
    public partial class SelectCar {
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
            ((SelectCar)o).OnSelectedCarChanged((CarObject)e.OldValue, (CarObject)e.NewValue);
        }

        private void OnSelectedCarChanged(CarObject oldValue, CarObject newValue) {
            if (_list != null) {
                _list.SelectedItem = newValue;
            }
        }

        private ISelectedItemPage<AcObjectNew> _list;

        private void OnFrameNavigated(object sender, NavigationEventArgs e) {
            if (_list != null) {
                _list.PropertyChanged -= List_PropertyChanged;
            }
            
            _list = ((ModernTab)sender).Frame.Content as ISelectedItemPage<AcObjectNew>;
            Logging.Debug("[SelectCar] OnFrameNavigated(): " + _list);
            if (_list == null) return;

            _list.SelectedItem = SelectedCar;
            _list.PropertyChanged += List_PropertyChanged;
            Logging.Debug("[SelectCar] OnFrameNavigated(): PropertyChanged set");
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            Logging.Debug("[SelectCar] List_PropertyChanged(): " + e.PropertyName);
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                SelectedCar = _list.SelectedItem as CarObject;
            }
        }
    }
}
