using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Pages.Dialogs {
    public partial class CarTransmissionLossSelector : INotifyPropertyChanged {
        public double DataTorque { get; }

        public double DataPower { get; }

        public double UiTorque => DataTorque * Multipler;

        public double UiPower => DataPower * Multipler;

        public CarObject Car { get; }

        private double _value;
        private double _multipler;

        public double Value {
            get => _value;
            set {
                value = value.Clamp(0d, 0.95d);
                if (value == _value) return;
                _value = value;
                _multipler = 1d / (1d - value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Multipler));
                OnPropertyChanged(nameof(UiPower));
                OnPropertyChanged(nameof(UiTorque));
                CacheStorage.Set(_key, value);
            }
        }

        public double Multipler => _multipler;

        private readonly string _key;

        public CarTransmissionLossSelector(CarObject car, double dataTorque, double dataPower) {
            DataTorque = dataTorque;
            DataPower = dataPower;
            Car = car;

            _key = ".CarTransmissionLossSelector:" + car.Id;
            Value = CacheStorage.Get(_key, 0.13d);

            InitializeComponent();
            DataContext = this;

            Buttons = new[] { OkButton, CancelButton };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
