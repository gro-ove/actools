using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        #region Properties
        private bool _wrapperUsed;

        public bool WrapperUsed {
            get => _wrapperUsed;
            set {
                if (Equals(value, _wrapperUsed)) return;
                _wrapperUsed = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int? _wrapperPort;

        public int? WrapperPort {
            get => _wrapperPort;
            set {
                if (Equals(value, _wrapperPort)) return;
                _wrapperPort = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _wrapperDescription;

        public string WrapperDescription {
            get => _wrapperDescription;
            set {
                if (Equals(value, _wrapperDescription)) return;
                _wrapperDescription = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _wrapperVerboseLog;

        public bool WrapperVerboseLog {
            get => _wrapperVerboseLog;
            set {
                if (Equals(value, _wrapperVerboseLog)) return;
                _wrapperVerboseLog = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private long _wrapperDownloadSpeedLimit;

        public long WrapperDownloadSpeedLimit {
            get => _wrapperDownloadSpeedLimit;
            set {
                if (Equals(value, _wrapperDownloadSpeedLimit)) return;
                _wrapperDownloadSpeedLimit = value;
                OnPropertyChanged(nameof(DisplayWrapperDownloadSpeedLimit));

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        public string DisplayWrapperDownloadSpeedLimit {
            get => _wrapperDownloadSpeedLimit <= 0 ? "None" :
                    _wrapperDownloadSpeedLimit.ToReadableSize();
            set => WrapperDownloadSpeedLimit = LocalizationHelper.TryParseReadableSize(value,
                    _wrapperDownloadSpeedLimit.ToReadableSize().Split(' ').LastOrDefault(), out long parsed) ? parsed : 0;
        }

        private bool _wrapperDownloadPasswordOnly;

        public bool WrapperDownloadPasswordOnly {
            get => _wrapperDownloadPasswordOnly;
            set {
                if (Equals(value, _wrapperDownloadPasswordOnly)) return;
                _wrapperDownloadPasswordOnly = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _wrapperPublishPasswordChecksum;

        public bool WrapperPublishPasswordChecksum {
            get => _wrapperPublishPasswordChecksum;
            set {
                if (Equals(value, _wrapperPublishPasswordChecksum)) return;
                _wrapperPublishPasswordChecksum = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        public string WrapperConfigFilename { get; private set; }
        public string WrapperContentDirectory { get; private set; }
        public string WrapperContentFilename { get; private set; }

        protected void InitializeWrapperLocations() {
            WrapperConfigFilename = Path.Combine(Location, "cm_wrapper_params.json");
            WrapperContentDirectory = Path.Combine(Location, "cm_content");
            WrapperContentFilename = Path.Combine(WrapperContentDirectory, "content.json");
        }

        private JObject _wrapperParamsJson;

        public void SaveWrapperParams() {
            _wrapperParamsJson = _wrapperParamsJson ?? new JObject();
            _wrapperParamsJson["enabled"] = WrapperUsed;
            _wrapperParamsJson["downloadSpeedLimit"] = WrapperDownloadSpeedLimit;
            _wrapperParamsJson["verboseLog"] = WrapperVerboseLog;
            _wrapperParamsJson["description"] = WrapperDescription;
            _wrapperParamsJson["downloadPasswordOnly"] = WrapperDownloadPasswordOnly;
            _wrapperParamsJson["publishPasswordChecksum"] = WrapperPublishPasswordChecksum;

            try {
                File.WriteAllText(WrapperConfigFilename, _wrapperParamsJson.ToString(Formatting.Indented));
            }catch (Exception e) {
                NonfatalError.Notify("Can’t save server wrapper params", e);
            }
        }

        public void LoadWrapperParams() {
            try {
                _wrapperParamsJson = File.Exists(WrapperConfigFilename) ?
                        JsonExtension.Parse(File.ReadAllText(WrapperConfigFilename)) : null;
            } catch (Exception e) {
                _wrapperParamsJson = null;
                Logging.Warning(e);
                AddError(AcErrorType.Data_JsonIsDamaged, Path.GetFileName(WrapperConfigFilename));
            }

            WrapperUsed = _wrapperParamsJson?.GetBoolValueOnly("enabled", true) ?? false;

            var obj = _wrapperParamsJson ?? new JObject();
            WrapperPort = obj.GetIntValueOnly("port", 80);
            WrapperDownloadSpeedLimit = (long)obj.GetDoubleValueOnly("downloadSpeedLimit", 1e6);
            WrapperVerboseLog = obj.GetBoolValueOnly("verboseLog", true);
            WrapperDescription = obj.GetStringValueOnly("description");
            WrapperDownloadPasswordOnly = obj.GetBoolValueOnly("downloadPasswordOnly", true);
            WrapperPublishPasswordChecksum = obj.GetBoolValueOnly("publishPasswordChecksum", true);
        }
        #endregion

        private bool _isWrapperConnected;

        public bool IsWrapperConnected {
            get => _isWrapperConnected;
            set {
                if (Equals(value, _isWrapperConnected)) return;
                _isWrapperConnected = value;
                OnPropertyChanged();
            }
        }

        private string _wrapperConnectionFailedReason;

        public string WrapperConnectionFailedReason {
            get => _wrapperConnectionFailedReason;
            set {
                if (Equals(value, _wrapperConnectionFailedReason)) return;
                _wrapperConnectionFailedReason = value;
                OnPropertyChanged();
            }
        }

        private string _wrapperIpAddress;

        [CanBeNull]
        public string WrapperIpAddress {
            get => _wrapperIpAddress;
            set {
                if (Equals(value, _wrapperIpAddress)) return;
                _wrapperIpAddress = value;
                OnPropertyChanged();
            }
        }

        private string _wrapperPassword;

        [CanBeNull]
        public string WrapperPassword {
            get => _wrapperPassword;
            set {
                if (Equals(value, _wrapperPassword)) return;
                _wrapperPassword = value;
                OnPropertyChanged();
            }
        }

        private AcServerStatus _wrapperAcServerStatus;

        public AcServerStatus WrapperAcServerStatus {
            get => _wrapperAcServerStatus;
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
