using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.LapTimes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    public class LapTimeWrapped : NotifyPropertyChanged {
        public LapTimeWrapped(LapTimeEntry entry) {
            Entry = entry;
        }

        public LapTimeEntry Entry { get; }

        private bool _preparedCar;

        private async Task PrepareCar() {
            if (!_preparedCar) {
                _preparedCar = true;
                Car = await CarsManager.Instance.GetByIdAsync(Entry.CarId);
            }
            Track = await TracksManager.Instance.GetLayoutByKunosIdAsync(Entry.TrackId);
        }

        private CarObject _car;

        [CanBeNull]
        public CarObject Car {
            get {
                PrepareCar().Forget();
                return _car;
            }
            set {
                if (Equals(value, _car)) return;
                _car = value;
                OnPropertyChanged();
            }
        }

        private bool _preparedTrack;

        private async Task PrepareTrack() {
            if (!_preparedTrack) {
                _preparedTrack = true;
                Track = await TracksManager.Instance.GetLayoutByKunosIdAsync(Entry.TrackId);
            }
        }

        private TrackObjectBase _track;

        [CanBeNull]
        public TrackObjectBase Track {
            get {
                PrepareTrack().Forget();
                return _track;
            }
            set {
                if (Equals(value, _track)) return;
                _track = value;
                OnPropertyChanged();
            }
        }
    }
}