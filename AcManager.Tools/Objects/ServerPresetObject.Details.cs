using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Helpers.Api;
using AcTools.DataFile;
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
        private bool _provideDetails;

        public bool ProvideDetails {
            get => _provideDetails;
            set {
                if (Equals(value, _provideDetails)) return;
                _provideDetails = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private ServerPresetDetailsMode _detailsMode;

        public ServerPresetDetailsMode DetailsMode {
            get => _detailsMode;
            set {
                if (Equals(value, _detailsMode)) return;
                _detailsMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _detailsNamePiece;

        public string DetailsNamePiece {
            get => _detailsNamePiece;
            set => Apply(value, ref _detailsNamePiece);
        }

        private Task EnsureDetailsNameIsActualAsync(IniFile ini) {
            var serverSection = ini["SERVER"];

            var assists = new JObject {
                [@"absState"] = serverSection.GetInt("ABS_ALLOWED", 0),
                [@"tcState"] = serverSection.GetInt("TC_ALLOWED", 0),
                [@"fuelRate"] = serverSection.GetInt("FUEL_RATE", 0),
                [@"damageMultiplier"] = serverSection.GetInt("DAMAGE_MULTIPLIER", 0),
                [@"tyreWearRate"] = serverSection.GetInt("TYRE_WEAR_RATE", 0),
                [@"allowedTyresOut"] = serverSection.GetInt("ALLOWED_TYRES_OUT", 0),
                [@"stabilityAllowed"] = serverSection.GetBool(@"STABILITY_ALLOWED", false),
                [@"autoclutchAllowed"] = serverSection.GetBool(@"AUTOCLUTCH_ALLOWED", false),
                [@"tyreBlanketsAllowed"] = serverSection.GetBool(@"TYRE_BLANKETS_ALLOWED", false),
                [@"forceVirtualMirror"] = serverSection.GetBool(@"FORCE_VIRTUAL_MIRROR", false),
            };

            var data = new JObject {

            };
        }

        private string _detailsDescription;

        public string DetailsDescription {
            get => _detailsDescription;
            set {
                if (Equals(value, _detailsDescription)) return;
                _detailsDescription = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _detailsDownloadPasswordOnly;

        public bool DetailsDownloadPasswordOnly {
            get => _detailsDownloadPasswordOnly;
            set {
                if (Equals(value, _detailsDownloadPasswordOnly)) return;
                _detailsDownloadPasswordOnly = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _detailsPublishPasswordChecksum;

        public bool DetailsPublishPasswordChecksum {
            get => _detailsPublishPasswordChecksum;
            set {
                if (Equals(value, _detailsPublishPasswordChecksum)) return;
                _detailsPublishPasswordChecksum = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private JObject _detailsContentJObject;

        [CanBeNull]
        public JObject DetailsContentJObject {
            get => _detailsContentJObject;
            set {
                if (Equals(value, _detailsContentJObject)) return;
                _detailsContentJObject = value;

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
            destination[@"enabled"] = ProvideDetails;
            destination[@"detailsMode"] = (int)DetailsMode;
            destination[@"port"] = WrapperPort;
            destination[@"downloadSpeedLimit"] = WrapperDownloadSpeedLimit;
            destination[@"verboseLog"] = WrapperVerboseLog;
            destination[@"description"] = DetailsDescription;
            destination[@"downloadPasswordOnly"] = DetailsDownloadPasswordOnly;
            destination[@"publishPasswordChecksum"] = DetailsPublishPasswordChecksum;
        }

        public event EventHandler SaveWrapperContent;

        public void SaveWrapperParams() {
            SetWrapperParams(ref _wrapperParamsJson);

            try {
                if (ProvideDetails || _wrapperLoaded) {
                    File.WriteAllText(WrapperConfigFilename, _wrapperParamsJson.ToString(Formatting.Indented));
                } else if (File.Exists(WrapperConfigFilename)) {
                    File.Delete(WrapperConfigFilename);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t save server wrapper params", e);
            }

            try {
                var jObj = DetailsContentJObject ?? new JObject();
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
                DetailsContentJObject = File.Exists(WrapperContentFilename) ?
                        JsonExtension.Parse(File.ReadAllText(WrapperContentFilename)) : null;
            } catch (Exception e) {
                DetailsContentJObject = null;
                Logging.Warning(e);
                AddError(AcErrorType.Data_JsonIsDamaged, Path.GetFileName(WrapperContentFilename));
            }

            var obj = _wrapperParamsJson ?? new JObject { [@"detailsMode"] = (int)ServerPresetDetailsMode.ViaNameIdentifier };
            ProvideDetails = _wrapperParamsJson?.GetBoolValueOnly("enabled", true) ?? false;
            DetailsMode = (ServerPresetDetailsMode)obj.GetIntValueOnly("detailsMode", (int)ServerPresetDetailsMode.ViaWrapper);
            DetailsDescription = obj.GetStringValueOnly("description");
            DetailsDownloadPasswordOnly = obj.GetBoolValueOnly("downloadPasswordOnly", true);
            DetailsPublishPasswordChecksum = obj.GetBoolValueOnly("publishPasswordChecksum", true);
            WrapperPort = obj.GetIntValueOnly("port", 80);
            WrapperDownloadSpeedLimit = (long)obj.GetDoubleValueOnly("downloadSpeedLimit", 1e6);
            WrapperVerboseLog = obj.GetBoolValueOnly("verboseLog", true);
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