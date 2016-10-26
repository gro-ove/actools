using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.LapTimes;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    public class LapTimeWrapped : NotifyPropertyChanged {
        public LapTimeWrapped(LapTimeEntry entry) {
            Entry = entry;
            Prepare().Forget();
        }

        private async Task Prepare() {
            Car = await CarsManager.Instance.GetByIdAsync(Entry.CarId);
            Track = await TracksManager.Instance.GetLayoutByKunosIdAsync(Entry.TrackId);
        }

        public LapTimeEntry Entry { get; }

        private CarObject _car;

        [CanBeNull]
        public CarObject Car {
            get { return _car; }
            set {
                if (Equals(value, _car)) return;
                _car = value;
                OnPropertyChanged();
            }
        }

        private TrackObjectBase _track;

        [CanBeNull]
        public TrackObjectBase Track {
            get { return _track; }
            set {
                if (Equals(value, _track)) return;
                _track = value;
                OnPropertyChanged();
            }
        }
    }
}