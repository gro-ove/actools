using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using AcManager.Tools.Objects;
using AcTools.Utils;

namespace AcManager.Pages.Dialogs {
    public partial class CarTransmissionLossSelector : INotifyPropertyChanged {
        public CarObject Car { get; }

        private int _value;

        public int Value {
            get { return _value; }
            set {
                value = value.Clamp(0, 100);
                if (value == _value) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        public CarTransmissionLossSelector(CarObject car) {
            Car = car;

            InitializeComponent();
            DataContext = this;

            Value = 13;
            Buttons = new[] { OkButton, CancelButton };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
