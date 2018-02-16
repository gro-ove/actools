using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
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
                    $"{_wrapperDownloadSpeedLimit.ToReadableSize()}/s";
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

        private JObject _wrapperContentJObject;

        [CanBeNull]
        public JObject WrapperContentJObject {
            get => _wrapperContentJObject;
            set {
                if (Equals(value, _wrapperContentJObject)) return;
                _wrapperContentJObject = value;

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

        [ContractAnnotation("destination:null => destination:notnull; destination:notnull => destination:notnull")]
        private void SetWrapperParams(ref JObject destination) {
            destination = destination ?? new JObject();
            destination["enabled"] = WrapperUsed;
            destination["port"] = WrapperPort;
            destination["downloadSpeedLimit"] = WrapperDownloadSpeedLimit;
            destination["verboseLog"] = WrapperVerboseLog;
            destination["description"] = WrapperDescription;
            destination["downloadPasswordOnly"] = WrapperDownloadPasswordOnly;
            destination["publishPasswordChecksum"] = WrapperPublishPasswordChecksum;
        }

        public event EventHandler SaveWrapperContent;

        public void SaveWrapperParams() {
            SetWrapperParams(ref _wrapperParamsJson);

            try {
                if (WrapperUsed || _wrapperLoaded) {
                    File.WriteAllText(WrapperConfigFilename, _wrapperParamsJson.ToString(Formatting.Indented));
                }else if (File.Exists(WrapperConfigFilename)) {
                    File.Delete(WrapperConfigFilename);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t save server wrapper params", e);
            }

            try {
                var jObj = WrapperContentJObject ?? new JObject();
                if (jObj.Count > 0) {
                    FileUtils.EnsureFileDirectoryExists(WrapperContentFilename);
                    File.WriteAllText(WrapperContentFilename, jObj.ToString(Formatting.Indented));
                } else if (File.Exists(WrapperContentFilename)) {
                    File.Delete(WrapperContentFilename);
                }

                SaveWrapperContent?.Invoke(this, EventArgs.Empty);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t save server wrapper content description", e);
            }
        }

        private bool _wrapperLoaded;

        public void LoadWrapperParams() {
            try {
                if (File.Exists(WrapperConfigFilename)) {
                    _wrapperParamsJson = JsonExtension.Parse(File.ReadAllText(WrapperConfigFilename));
                    _wrapperLoaded = true;
                } else {
                    _wrapperParamsJson = null;
                    _wrapperLoaded = false;
                }
            } catch (Exception e) {
                _wrapperParamsJson = null;
                _wrapperLoaded = false;
                Logging.Warning(e);
                AddError(AcErrorType.Data_JsonIsDamaged, Path.GetFileName(WrapperConfigFilename));
            }

            try {
                WrapperContentJObject = File.Exists(WrapperContentFilename) ?
                        JsonExtension.Parse(File.ReadAllText(WrapperContentFilename)) : null;
            } catch (Exception e) {
                WrapperContentJObject = null;
                Logging.Warning(e);
                AddError(AcErrorType.Data_JsonIsDamaged, Path.GetFileName(WrapperContentFilename));
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
            set => Apply(value, ref _isWrapperConnected);
        }

        private string _wrapperConnectionFailedReason;

        public string WrapperConnectionFailedReason {
            get => _wrapperConnectionFailedReason;
            set => Apply(value, ref _wrapperConnectionFailedReason);
        }

        private string _wrapperIpAddress;

        [CanBeNull]
        public string WrapperIpAddress {
            get => _wrapperIpAddress;
            set => Apply(value, ref _wrapperIpAddress);
        }

        private string _wrapperPassword;

        [CanBeNull]
        public string WrapperPassword {
            get => _wrapperPassword;
            set => Apply(value, ref _wrapperPassword);
        }

        private AcServerStatus _wrapperAcServerStatus;

        public AcServerStatus WrapperAcServerStatus {
            get => _wrapperAcServerStatus;
            set => Apply(value, ref _wrapperAcServerStatus);
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
