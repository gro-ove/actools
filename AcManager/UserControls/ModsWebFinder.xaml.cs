using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.UserControls {
    public partial class ModsWebFinder {
        public ViewModel Model => (ViewModel)DataContext;

        public ModsWebFinder() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            private string _value;

            [CanBeNull]
            public string Value {
                get => _value;
                set => Apply(value, ref _value);
            }
        }
    }
}