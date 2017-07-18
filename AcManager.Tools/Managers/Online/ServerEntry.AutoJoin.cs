using System.Linq;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private bool _autoJoinAvailable;

        public bool AutoJoinAvailable {
            get { return _autoJoinAvailable; }
            set {
                if (Equals(value, _autoJoinAvailable)) return;
                _autoJoinAvailable = value;
                OnPropertyChanged();
            }
        }

        private bool _autoJoinAnyCarAvailable;

        public bool AutoJoinAnyCarAvailable {
            get { return _autoJoinAnyCarAvailable; }
            set {
                if (Equals(value, _autoJoinAnyCarAvailable)) return;
                _autoJoinAnyCarAvailable = value;
                OnPropertyChanged();
            }
        }

        public bool IsAutoJoinReady(bool anyCar) {
            return Status == ServerStatus.Ready && !IsBookedForPlayer && !BookingMode && (anyCar ? Cars?.Any(x => x.IsAvailable) == true : IsAvailable);
        }

        private void UpdateAutoJoinAvailable() {
            AutoJoinAvailable = Status == ServerStatus.Ready && !IsBookedForPlayer && !BookingMode && SelectedCarEntry?.IsAvailable == false;
            AutoJoinAnyCarAvailable = Status == ServerStatus.Ready && !IsBookedForPlayer && !BookingMode && Cars?.Count > 1 && Capacity == ConnectedDrivers;
        }
    }
}
