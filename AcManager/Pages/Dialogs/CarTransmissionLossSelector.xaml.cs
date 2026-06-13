using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public partial class CarTransmissionLossSelector : INotifyPropertyChanged {
        public double DataTorque { get; }

        public double DataPower { get; }

        public double UiTorque => DataTorque * Multiplier;

        public double UiPower => DataPower * Multiplier;

        public CarObject Car { get; }

        private double _value = double.NaN;
        private double _multiplier;

        public double Value {
            get => _value;
            set {
                value = value.Clamp(0d, 0.95d);
                if (value == _value) return;
                _value = value;
                _multiplier = 1d / (1d - value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Multiplier));
                OnPropertyChanged(nameof(UiPower));
                OnPropertyChanged(nameof(UiTorque));
                CacheStorage.Set(_key, value);
            }
        }

        public double Multiplier => _multiplier;

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
