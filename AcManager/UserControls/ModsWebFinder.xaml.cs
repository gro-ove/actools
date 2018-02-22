using FirstFloor.ModernUI.Presentation;

namespace AcManager.UserControls {
    public partial class ModsWebFinder {
        public ViewModel Model => (ViewModel)DataContext;

        public ModsWebFinder() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() { }

            private string _value;

            public string Value {
                get => _value;
                set => Apply(value, ref _value);
            }
        }
    }
}