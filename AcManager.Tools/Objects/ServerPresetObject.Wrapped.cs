using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.VisualBasic.Logging;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        private bool _isWrapperConnected;

        public bool IsWrapperConnected {
            get { return _isWrapperConnected; }
            set {
                if (Equals(value, _isWrapperConnected)) return;
                _isWrapperConnected = value;
                OnPropertyChanged();
            }
        }

        private string _wrapperConnectionFailedReason;

        public string WrapperConnectionFailedReason {
            get { return _wrapperConnectionFailedReason; }
            set {
                if (Equals(value, _wrapperConnectionFailedReason)) return;
                _wrapperConnectionFailedReason = value;
                OnPropertyChanged();
            }
        }

        private string _wrapperIpAddress;

        [CanBeNull]
        public string WrapperIpAddress {
            get { return _wrapperIpAddress; }
            set {
                if (Equals(value, _wrapperIpAddress)) return;
                _wrapperIpAddress = value;
                OnPropertyChanged();
            }
        }

        private int? _wrapperPort;

        public int? WrapperPort {
            get { return _wrapperPort; }
            set {
                if (Equals(value, _wrapperPort)) return;
                _wrapperPort = value;
                OnPropertyChanged();
            }
        }

        private string _wrapperPassword;

        [CanBeNull]
        public string WrapperPassword {
            get { return _wrapperPassword; }
            set {
                if (Equals(value, _wrapperPassword)) return;
                _wrapperPassword = value;
                OnPropertyChanged();
            }
        }

        private AcServerStatus _wrapperAcServerStatus;

        public AcServerStatus WrapperAcServerStatus {
            get { return _wrapperAcServerStatus; }
            set {
                if (Equals(value, _wrapperAcServerStatus)) return;
                _wrapperAcServerStatus = value;
                OnPropertyChanged();
            }
        }

        private AsyncCommand _connectWrapperCommand;

        public AsyncCommand ConnectWrapperCommand => _connectWrapperCommand ?? (_connectWrapperCommand = new AsyncCommand(async () => {
            if (WrapperPort == null) return; // TODO?
            try {
                WrapperAcServerStatus = await AcServerWrapperApi.GetCurrentStateAsync(WrapperIpAddress, WrapperPort.Value, WrapperPassword);
                IsWrapperConnected = true;
            } catch (Exception e) {
                WrapperConnectionFailedReason = e.Message;
                WrapperAcServerStatus = null;
                IsWrapperConnected = false;
            }
        }, () => !_isWrapperConnected && !string.IsNullOrWhiteSpace(_wrapperIpAddress) && !string.IsNullOrWhiteSpace(_wrapperPassword) && WrapperPort != null));
    }
}
