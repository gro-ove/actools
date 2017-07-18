using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers {
    public class AcContext : NotifyPropertyChanged {
        public static AcContext Instance { get; } =  new AcContext();

        private AcContext(){}

        private CarObject _currentCar;

        public CarObject CurrentCar {
            get { return _currentCar; }
            set {
                if (value == null) return;
                if (Equals(value, _currentCar)) return;
                _currentCar = value;
                OnPropertyChanged();
            }
        }

        private TrackObjectBase _currentTrack;

        public TrackObjectBase CurrentTrack {
            get { return _currentTrack; }
            set {
                if (value == null) return;
                if (Equals(value, _currentTrack)) return;
                _currentTrack = value;
                OnPropertyChanged();
            }
        }
    }
}