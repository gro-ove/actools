using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public static class ServerDetailsUtils {
        private const string ChecksumChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [ContractAnnotation(@"password: null => null; password: notnull => notnull")]
        public static string PasswordChecksum(string password, string serverName = null) {
            return password == null ? null : (@"apatosaur" + serverName + password).GetChecksum().ToLowerInvariant();
        }

        [ContractAnnotation(@"password: null => null; password: notnull => notnull")]
        public static string EncryptedContentKey(string password) {
            return password == null ? null : (@"tgys3cqpcwpbssphb0j46tak8ykldaub" + password).GetChecksum().ToCutBase64();
        }

        public static string InsertDetailsId([NotNull] string namePiece) {
            var s = ChecksumChars[(117 + namePiece.Sum(x => (int)x)) % ChecksumChars.Length];
            return $@"x:{namePiece}{s}";
        }

        public static string ExtractDetailsId(string name, out string detailsId) {
            try {
                var index = name.LastIndexOf(@"x:", StringComparison.OrdinalIgnoreCase);
                if (index >= 1 && !char.IsLetterOrDigit(name[index - 1])) {
                    int l = 0, u = 117;
                    for (var i = index + 2; i < name.Length && IsIdChar(name[i]); i++, l++) {
                        u += name[i];
                    }

                    if (l >= 2) {
                        var s = name[index + l + 1];
                        if (ChecksumChars[(u - s) % ChecksumChars.Length] == s) {
                            detailsId = name.Substring(index + 2, l - 1);
                            return name.Substring(0, index).TrimEnd() + (index + l + 3 < name.Length ? name.Substring(index + l + 2) : "");
                        }
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);

            }

            detailsId = null;
            return name;

            bool IsIdChar(char c) {
                return char.IsLetterOrDigit(c) || c == '-' || c == '_';
            }
        }
    }

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

        [CanBeNull]
        public string DetailsNamePiece {
            get => _detailsNamePiece;
            set => Apply(value, ref _detailsNamePiece);
        }

        private async Task EnsureDetailsNameIsActualAsync(IniFile ini) {
            if (!ProvideDetails || DetailsMode != ServerPresetDetailsMode.ViaNameIdentifier) {
                DetailsNamePiece = null;
                return;
            }

            // var serverSection = ini["SERVER"];
            // var geoParams = await IpGeoProvider.GetAsync();

            var data = new ServerInformationExtra {
                FrequencyHz = SendIntervalHz,
                Durations = Sessions.Select(x => (long)x.Time.TotalSeconds.RoundToInt()).ToArray(),
                Assists = new ServerInformationExtendedAssists {
                    AbsState = Abs,
                    TractionControlState = TractionControl,
                    FuelRate = FuelRate,
                    DamageMultiplier = DamageRate,
                    TyreWearRate = TyreWearRate,
                    AllowedTyresOut = AllowTyresOut,
                    StabilityAllowed = StabilityControl,
                    AutoclutchAllowed = AutoClutch,
                    TyreBlankets = TyreBlankets,
                    ForceVirtualMirror = ForceVirtualMirror,
                },
            };

            if (Password != null || AdminPassword != null) {
                data.PasswordChecksum = new[] {
                    ServerDetailsUtils.PasswordChecksum(Password),
                    ServerDetailsUtils.PasswordChecksum(AdminPassword),
                };
            }

            // No need to store geo params: preset might be used somewhere else!
            /*if (geoParams != null) {
                data.Country = new[] {
                    AcStringValues.GetCountryFromId(geoParams.Country),
                    geoParams.Country
                };
                data.City = geoParams.City;
            }*/

            var weather = Weather?.FirstOrDefault();
            if (weather != null) {
                data.AmbientTemperature = weather.BaseAmbientTemperature;
                data.RoadTemperature = weather.BaseRoadTemperature;
                data.WeatherId = weather.WeatherId;
                data.WindSpeed = (weather.WindSpeedMin + weather.WindSpeedMax) / 2d;
                data.WindDirection = weather.WindDirection;
            }

            if (DynamicTrackEnabled) {
                data.Grip = TrackProperties.SessionStart;
                data.GripTransfer = TrackProperties.SessionTransfer;
            }

            if (MaxCollisionsPerKm != -1) {
                data.MaxContactsPerKm = MaxCollisionsPerKm;
            }

            if (TrackLayoutId == null && TrackId.IndexOf('-') == -1) {
                data.TrackBase = TrackId;
            }

            if (!string.IsNullOrWhiteSpace(DetailsDescription)) {
                data.Description = DetailsDescription;
            }

            if (DetailsContentJObject != null) {
                if (DetailsDownloadPasswordOnly && !string.IsNullOrWhiteSpace(Password)) {
                    data.ContentPrivate = StringCipher.Encrypt(DetailsContentJObject.ToString(Formatting.None), ServerDetailsUtils.EncryptedContentKey(Password));
                } else {
                    data.Content = DetailsContentJObject;
                }
            }

            try {
                Apply(await CmApiProvider.PostOnlineDataAsync(data));
            } catch (Exception e) {
                Logging.Warning(e);
                Apply(null);
            }

            void Apply(string namePiece) {
                if (namePiece != null) {
                    ini["__CM_SERVER"].Set("NAME", Name);
                    ini["__CM_SERVER"].Set("DETAILS_ID", namePiece);
                    ini["SERVER"].Set("NAME",
                            (char.IsLetterOrDigit(Name?.LastOrDefault() ?? '.') ? Name + @" " : Name) + ServerDetailsUtils.InsertDetailsId(namePiece));
                }

                DetailsNamePiece = namePiece;
            }
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
            destination[@"publishPasswordChecksum"] = true;
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