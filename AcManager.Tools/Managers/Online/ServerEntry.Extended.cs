using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private int? _portExtended;

        /// <summary>
        /// For extended information via CM-wrapper.
        /// </summary>
        public int? PortExtended {
            get => _portExtended;
            set {
                if (Equals(value, _portExtended)) return;
                _portExtended = value;
                OnPropertyChanged();
            }
        }

        private bool _extendedMode;

        public bool ExtendedMode {
            get => _extendedMode;
            set {
                if (Equals(value, _extendedMode)) return;
                _extendedMode = value;
                OnPropertyChanged();
            }
        }

        private string _city;

        public string City {
            get => _city;
            set {
                if (Equals(value, _city)) return;
                _city = value;
                OnPropertyChanged();
            }
        }

        private string _description;

        public string Description {
            get => _description;
            set {
                if (Equals(value, _description)) return;
                _description = value;
                OnPropertyChanged();
            }
        }

        private string _trackBaseId;

        [CanBeNull]
        public string TrackBaseId {
            get => _trackBaseId;
            set {
                if (Equals(value, _trackBaseId)) return;
                _trackBaseId = value;
                OnPropertyChanged();
            }
        }

        private int _frequencyHz;

        public int FrequencyHz {
            get => _frequencyHz;
            set {
                if (Equals(value, _frequencyHz)) return;
                _frequencyHz = value;
                OnPropertyChanged();
            }
        }

        private string _weatherId;

        [CanBeNull]
        public string WeatherId {
            get => _weatherId;
            set {
                if (Equals(value, _weatherId)) return;
                _weatherId = value;

                _weatherObject = null;
                _weatherObjectSet = value == null;

                OnPropertyChanged();
                OnPropertyChanged(nameof(WeatherObject));
                OnPropertyChanged(nameof(WeatherDisplayName));
            }
        }

        private bool _weatherObjectSet = true;
        private WeatherObject _weatherObject;

        [CanBeNull]
        public WeatherObject WeatherObject {
            get {
                if (!_weatherObjectSet) {
                    _weatherObjectSet = true;
                    _weatherObject = _weatherId == null ? null : WeatherManager.Instance.GetById(_weatherId);
                }
                return _weatherObject;
            }
        }

        public string WeatherDisplayName => WeatherObject?.DisplayName ?? WeatherId;

        private double? _ambientTemperature;

        public double? AmbientTemperature {
            get => _ambientTemperature;
            set {
                if (Equals(value, _ambientTemperature)) return;
                _ambientTemperature = value;
                OnPropertyChanged();
            }
        }

        private double? _roadTemperature;

        public double? RoadTemperature {
            get => _roadTemperature;
            set {
                if (Equals(value, _roadTemperature)) return;
                _roadTemperature = value;
                OnPropertyChanged();
            }
        }

        private double? _windDirection;

        public double? WindDirection {
            get => _windDirection;
            set {
                if (Equals(value, _windDirection)) return;
                _windDirection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayWindDirection));
            }
        }

        public string DisplayWindDirection => _windDirection?.ToDisplayWindDirection();

        private double? _windSpeed;

        public double? WindSpeed {
            get => _windSpeed;
            set {
                if (Equals(value, _windSpeed)) return;
                _windSpeed = value;
                OnPropertyChanged();
            }
        }

        private string[] _passwordChecksum;

        [CanBeNull]
        public string[] PasswordChecksum {
            get => _passwordChecksum;
            set {
                if (Equals(value, _passwordChecksum)) return;
                _passwordChecksum = value;
                OnPropertyChanged();
                InvalidatePasswordIsWrong();
            }
        }

        private double? _grip;

        public double? Grip {
            get => _grip;
            set {
                if (Equals(value, _grip)) return;
                _grip = value;
                OnPropertyChanged();
            }
        }

        private double? _gripTransfer;

        public double? GripTransfer {
            get => _gripTransfer;
            set {
                if (Equals(value, _gripTransfer)) return;
                _gripTransfer = value;
                OnPropertyChanged();
            }
        }

        private double? _maxContactsPerKm;

        public double? MaxContactsPerKm {
            get => _maxContactsPerKm;
            set {
                if (Equals(value, _maxContactsPerKm)) return;
                _maxContactsPerKm = value;
                OnPropertyChanged();
            }
        }

        private ServerInformationExtendedAssists _assistsInformation;

        public ServerInformationExtendedAssists AssistsInformation {
            get => _assistsInformation;
            set {
                if (Equals(value, _assistsInformation)) return;
                _assistsInformation = value;
                OnPropertyChanged();
            }
        }

        public enum IsAbleToInstallMissingContent {
            NoMissingContent,
            NotAbleTo,
            Partially,
            AllOfIt,
            Updates
        }

        private void UpdateValuesExtended([CanBeNull] ServerInformationExtended extended) {
            if (extended == null) {
                extended = new ServerInformationExtended();
                ExtendedMode = false;
            } else {
                ExtendedMode = true;
            }

            City = extended.City;
            PasswordChecksum = extended.PasswordChecksum;
            AssistsInformation = extended.Assists;
            Description = extended.Description;
            WeatherId = extended.WeatherId;
            AmbientTemperature = extended.AmbientTemperature;
            RoadTemperature = extended.RoadTemperature;
            WindDirection = extended.WindDirection;
            WindSpeed = extended.WindSpeed;
            Grip = extended.Grip;
            GripTransfer = extended.GripTransfer;
            TrackBaseId = extended.TrackBase?.Trim();
            FrequencyHz = extended.FrequencyHz ?? 0;
            MaxContactsPerKm = extended.MaxContactsPerKm;
            _missingContentReferences = extended.Content;
        }

        private bool CheckPasswordChecksum() {
            if (PasswordChecksum == null) return true;
            using (var sha1 = SHA1.Create()) {
                return PasswordChecksum.ArrayContains(sha1.ComputeHash(Encoding.UTF8.GetBytes("apatosaur" + ActualName + Password))
                                                     .ToHexString().ToLowerInvariant());
            }
        }

        private bool IsPasswordValid() {
            return !PasswordRequired || !string.IsNullOrEmpty(Password) && !PasswordWasWrong && CheckPasswordChecksum();
        }

        #region Missing content
        [CanBeNull]
        private JObject _missingContentReferences;

        private IEnumerable<Task> InstallMissingContentTasks() {
            var mref = _missingContentReferences;
            if (mref == null) {
                throw new Exception("MREFs are not defined");
            }

            var passwordPostfix = Lazier.Create(() => {
                using (var sha1 = SHA1.Create()) {
                    return "?password=" + sha1.ComputeHash(Encoding.UTF8.GetBytes("tanidolizedhoatzin" + Password))
                               .ToHexString().ToLowerInvariant();
                }
            });

            var cars = mref["cars"] as JObject;
            if (cars != null) {
                foreach (var carPair in cars) {
                    var car = CarsManager.Instance.GetById(carPair.Key);

                    if (car == null || carPair.Value.GetStringValueOnly("version").IsVersionNewerThan(car.Version)) {
                        if (!IsAvailableToInstall(carPair.Value)) continue;
                        var url = carPair.Value.GetStringValueOnly("url") ??
                                $"http://{Ip}:{PortExtended}/content/car/{carPair.Key}{passwordPostfix.Value}";
                        yield return ContentInstallationManager.Instance.InstallAsync(url, new ContentInstallationParams {
                            FallbackId = carPair.Key,
                            Checksum = carPair.Value.GetStringValueOnly("checksum")
                        });
                    } else {
                        var skins = carPair.Value["skins"] as JObject;
                        if (skins != null) {
                            foreach (var skinPair in skins) {
                                if (car.SkinsManager.GetWrapperById(skinPair.Key) != null ||
                                        !IsAvailableToInstall(skinPair.Value)) continue;

                                var url = skinPair.Value.GetStringValueOnly("url") ??
                                        $"http://{Ip}:{PortExtended}/content/skin/{carPair.Key}/{skinPair.Key}{passwordPostfix.Value}";
                                yield return ContentInstallationManager.Instance.InstallAsync(url, new ContentInstallationParams {
                                    CarId = carPair.Key,
                                    FallbackId = skinPair.Key,
                                    Checksum = skinPair.Value.GetStringValueOnly("checksum")
                                });
                            }
                        }
                    }
                }
            }

            var weather = mref["weather"] as JObject;
            if (weather != null) {
                foreach (var weatherPair in weather) {
                    if (WeatherManager.Instance.GetWrapperById(weatherPair.Key) != null ||
                            !IsAvailableToInstall(weatherPair.Value)) continue;

                    var url = weatherPair.Value.GetStringValueOnly("url") ??
                            $"http://{Ip}:{PortExtended}/content/weather/{weatherPair.Key}{passwordPostfix.Value}";
                    yield return ContentInstallationManager.Instance.InstallAsync(url, new ContentInstallationParams {
                        FallbackId = weatherPair.Key,
                        Checksum = weatherPair.Value.GetStringValueOnly("checksum")
                    });
                }
            }

            var track = mref["track"] as JObject;
            if (track != null && (Track == null || track.GetStringValueOnly("version").IsVersionNewerThan(Track.Version)) && IsAvailableToInstall(track)) {
                var url = track.GetStringValueOnly("url") ??
                        $"http://{Ip}:{PortExtended}/content/track{passwordPostfix.Value}";
                yield return ContentInstallationManager.Instance.InstallAsync(url, new ContentInstallationParams {
                    FallbackId = TrackBaseId,
                    Checksum = track.GetStringValueOnly("checksum")
                });
            }
        }

        private DelegateCommand _installMissingContentCommand;

        public DelegateCommand InstallMissingContentCommand => _installMissingContentCommand ?? (_installMissingContentCommand = new DelegateCommand(async () => {
            if (_missingContentReferences?.GetBoolValueOnly("password") == true && !IsPasswordValid()) {
                ModernDialog.ShowMessage("Can’t install content, password is required.", "Can’t install content", MessageBoxButton.OK);
                return;
            }

            try {
                await InstallMissingContentTasks().WhenAll();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t install content", e);
            }
        }, () => IsAbleToInstallMissingContentState == IsAbleToInstallMissingContent.Partially ||
                IsAbleToInstallMissingContentState == IsAbleToInstallMissingContent.AllOfIt ||
                IsAbleToInstallMissingContentState == IsAbleToInstallMissingContent.Updates));

        private IsAbleToInstallMissingContent _isAbleToInstallMissingContentState = IsAbleToInstallMissingContent.NoMissingContent;

        public IsAbleToInstallMissingContent IsAbleToInstallMissingContentState {
            get => _isAbleToInstallMissingContentState;
            set {
                if (Equals(value, _isAbleToInstallMissingContentState)) return;
                _isAbleToInstallMissingContentState = value;
                OnPropertyChanged();
                _installMissingContentCommand?.RaiseCanExecuteChanged();
            }
        }

        [Pure]
        private static bool IsAvailableToInstall([CanBeNull] JToken token) {
            if (token == null) return false;
            if ((string)token["url"] != null) return true;

            try {
                if ((bool?)token["direct"] == false) return false;
            } catch (Exception e) {
                Logging.Warning(e.Message);
            }

            return true;
        }

        private static IEnumerable<string> GetKeys([CanBeNull] JToken token) {
            var obj = token as JObject;
            if (obj == null) yield break;

            foreach (var p in obj) {
                if (IsAvailableToInstall(p.Value)) {
                    yield return p.Key;
                }
            }
        }

        private string GetRequiredCarVersion(string carId) {
            return _missingContentReferences?["cars"]?[carId]?.GetStringValueOnly("version");
        }

        private string GetRequiredTrackVersion() {
            return _missingContentReferences?["track"]?.GetStringValueOnly("version");
        }

        private ServerStatus? UpdateMissingContentExtended(bool alreadyMissingSomething) {
            _updateMissingExtendedErrors.Clear();
            _carVersionIsWrong = false;
            _trackVersionIsWrong = false;

            var mref = _missingContentReferences;
            if (mref == null) {
                IsAbleToInstallMissingContentState = alreadyMissingSomething ?
                        IsAbleToInstallMissingContent.NotAbleTo :
                        IsAbleToInstallMissingContent.NoMissingContent;
                return null;
            }

            var missingSomething = false;
            var somethingIsObsolete = false;

            var cars = mref["cars"] as JObject;
            if (cars != null) {
                var missingSkins = new List<string>();

                foreach (var carPair in cars) {
                    var carId = carPair.Key;
                    var car = CarsManager.Instance.GetById(carId);
                    if (car == null) continue;

                    var version = carPair.Value.GetStringValueOnly("version");
                    if (version.IsVersionNewerThan(car.Version)) {
                        _updateMissingExtendedErrors.Add($"{car.DisplayName} is obsolete (installed: {car.Version}; server runs: {version})");
                        _carVersionIsWrong = true;
                        somethingIsObsolete = true;
                    }

                    var skins = carPair.Value["skins"] as JObject;
                    if (skins != null) {
                        foreach (var skinPair in skins) {
                            if (IsAvailableToInstall(skinPair.Value) && car.SkinsManager.GetWrapperById(skinPair.Key) == null) {
                                missingSkins.Add($"“{skinPair.Key}” ({car.DisplayName})");
                            }
                        }
                    }
                }

                if (missingSkins.Any()) {
                    _updateMissingExtendedErrors.Add(string.Format(
                            missingSkins.Count == 1 ? ToolsStrings.Online_Server_CarSkinIsMissing : ToolsStrings.Online_Server_CarSkinsAreMissing,
                            missingSkins.JoinToReadableString()));
                    missingSomething = true;
                }
            }

            if (Track != null) {
                var track = mref["track"];
                var version = track?.GetStringValueOnly("version");
                if (IsAvailableToInstall(track) && version.IsVersionNewerThan(Track.Version)) {
                    _updateMissingExtendedErrors.Add($"{Track.Name} is obsolete (installed: {Track.Version}; server runs: {version})");
                    _trackVersionIsWrong = true;
                    somethingIsObsolete = true;
                }
            }

            var missingWeatherIds = GetKeys(mref["weather"]).Where(x => WeatherManager.Instance.GetWrapperById(x) == null).ToList();
            if (missingWeatherIds.Any()) {
                _updateMissingExtendedErrors.Add(string.Format(ToolsStrings.Online_Server_WeatherIsMissing,
                        missingWeatherIds.Select(x => $"“{x}”").JoinToReadableString()));
                missingSomething = true;
            }

            IsAbleToInstallMissingContent state;
            if (!missingSomething && !alreadyMissingSomething) {
                state = IsAbleToInstallMissingContent.NoMissingContent;
            } else {
                var allCarsAvailable = true;
                var trackAvailable = true;

                var missingCarsIds = Cars?.Where(x => !x.CarExists).Select(x => x.Id).ToList();
                if (missingCarsIds?.Count > 0) {
                    var availableCarsIds = GetKeys(mref["cars"]).ToList();
                    var missingButAvailable = missingCarsIds.Where(availableCarsIds.Contains).ToList();
                    allCarsAvailable = missingCarsIds.Count == missingButAvailable.Count;
                }

                if (Track == null) {
                    trackAvailable = IsAvailableToInstall(mref["track"]);
                }

                state = allCarsAvailable && trackAvailable
                        ? IsAbleToInstallMissingContent.AllOfIt : IsAbleToInstallMissingContent.Partially;
            }

            if (somethingIsObsolete && state == IsAbleToInstallMissingContent.NoMissingContent) {
                state = IsAbleToInstallMissingContent.Updates;
            }

            IsAbleToInstallMissingContentState = state;
            return missingSomething ? ServerStatus.MissingContent : (ServerStatus?)null;
        }
        #endregion
    }
}
