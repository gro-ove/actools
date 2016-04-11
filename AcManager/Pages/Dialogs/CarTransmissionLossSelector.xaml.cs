using System.ComponentModel;
using System.Runtime.CompilerServices;
using AcManager.Annotations;
using AcManager.Tools.Objects;

namespace AcManager.Pages.Dialogs {
    public partial class CarTransmissionLossSelector : INotifyPropertyChanged {
        private int _value;

        public int Value {
            get { return _value; }
            private set {
                if (value == _value) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        public CarTransmissionLossSelector(CarObject car) {
            InitializeComponent();
            DataContext = this;

            Title = "Transmission Loss for " + car.DisplayName;

            Value = 13;
            Buttons = new [] { OkButton, CancelButton };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
