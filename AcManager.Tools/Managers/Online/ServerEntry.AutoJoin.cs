using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

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
