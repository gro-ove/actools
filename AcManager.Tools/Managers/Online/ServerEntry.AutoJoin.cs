using System.Linq;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private bool _autoJoinAvailable;

        public bool AutoJoinAvailable {
            get { return _autoJoinAvailable; }
            set => Apply(value, ref _autoJoinAvailable);
        }

        private bool _autoJoinAnyCarAvailable;

        public bool AutoJoinAnyCarAvailable {
            get { return _autoJoinAnyCarAvailable; }
            set => Apply(value, ref _autoJoinAnyCarAvailable);
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
