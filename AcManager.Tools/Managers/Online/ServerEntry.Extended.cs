using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private int? _portExtended;

        /// <summary>
        /// For extended information via CM-wrapper.
        /// </summary>
        public int? PortExtended {
            get { return _portExtended; }
            set {
                if (Equals(value, _portExtended)) return;
                _portExtended = value;
                OnPropertyChanged();
            }
        }

        private bool _extendedMode;

        public bool ExtendedMode {
            get { return _extendedMode; }
            set {
                if (Equals(value, _extendedMode)) return;
                _extendedMode = value;
                OnPropertyChanged();
            }
        }

        private string _city;

        public string City {
            get { return _city; }
            set {
                if (Equals(value, _city)) return;
                _city = value;
                OnPropertyChanged();
            }
        }

        private string _trackBaseId;

        [CanBeNull]
        public string TrackBaseId {
            get { return _trackBaseId; }
            set {
                if (Equals(value, _trackBaseId)) return;
                _trackBaseId = value;
                OnPropertyChanged();
            }
        }

        private int _frequencyHz;

        public int FrequencyHz {
            get { return _frequencyHz; }
            set {
                if (Equals(value, _frequencyHz)) return;
                _frequencyHz = value;
                OnPropertyChanged();
            }
        }

        private string _weatherId;

        [CanBeNull]
        public string WeatherId {
            get { return _weatherId; }
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
            get { return _ambientTemperature; }
            set {
                if (Equals(value, _ambientTemperature)) return;
                _ambientTemperature = value;
                OnPropertyChanged();
            }
        }

        private double? _roadTemperature;

        public double? RoadTemperature {
            get { return _roadTemperature; }
            set {
                if (Equals(value, _roadTemperature)) return;
                _roadTemperature = value;
                OnPropertyChanged();
            }
        }

        private double? _windDirection;

        public double? WindDirection {
            get { return _windDirection; }
            set {
                if (Equals(value, _windDirection)) return;
                _windDirection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayWindDirection));
            }
        }

        public string DisplayWindDirection {
            get {
                if (!_windDirection.HasValue) return null;
                switch ((_windDirection.Value / 22.5).RoundToInt()) {
                    case 0:
                    case 16:
                        return "N";
                    case 1:
                        return "NNE";
                    case 2:
                        return "NE";
                    case 3:
                        return "ENE";
                    case 4:
                        return "E";
                    case 5:
                        return "ESE";
                    case 6:
                        return "SE";
                    case 7:
                        return "SSE";
                    case 8:
                        return "S";
                    case 9:
                        return "SSW";
                    case 10:
                        return "SW";
                    case 11:
                        return "WSW";
                    case 12:
                        return "W";
                    case 13:
                        return "WNW";
                    case 14:
                        return "NW";
                    case 15:
                        return "NNW";
                    default:
                        return "?";
                }
            }
        }

        private double? _windSpeed;

        public double? WindSpeed {
            get { return _windSpeed; }
            set {
                if (Equals(value, _windSpeed)) return;
                _windSpeed = value;
                OnPropertyChanged();
            }
        }

        private double? _grip;

        public double? Grip {
            get { return _grip; }
            set {
                if (Equals(value, _grip)) return;
                _grip = value;
                OnPropertyChanged();
            }
        }

        private double? _gripTransfer;

        public double? GripTransfer {
            get { return _gripTransfer; }
            set {
                if (Equals(value, _gripTransfer)) return;
                _gripTransfer = value;
                OnPropertyChanged();
            }
        }

        public enum IsAbleToInstallMissingContent {
            NoMissingContent,
            NotAbleTo,
            Partially,
            AllOfIt
        }

        private void UpdateValuesExtended([CanBeNull] ServerInformationExtended extended) {
            if (extended == null) {
                extended = new ServerInformationExtended();
                ExtendedMode = false;
            } else {
                ExtendedMode = true;
            }

            City = extended.City;
            WeatherId = extended.WeatherId;
            AmbientTemperature = extended.AmbientTemperature;
            RoadTemperature = extended.RoadTemperature;
            WindDirection = extended.WindDirection;
            WindSpeed = extended.WindSpeed;
            Grip = extended.Grip;
            GripTransfer = extended.GripTransfer;
            TrackBaseId = extended.TrackBase;
            FrequencyHz = extended.FrequencyHz ?? 0;
            _missingContentReferences = extended.Content;
        }

        #region Missing content
        [CanBeNull]
        private JObject _missingContentReferences;

        private AsyncCommand _installMissingContentCommand;

        public AsyncCommand InstallMissingContentCommand => _installMissingContentCommand ?? (_installMissingContentCommand = new AsyncCommand(async () => {
            
        }, () => IsAbleToInstallMissingContentState == IsAbleToInstallMissingContent.Partially ||
                IsAbleToInstallMissingContentState == IsAbleToInstallMissingContent.AllOfIt));

        private IsAbleToInstallMissingContent _isAbleToInstallMissingContentState = IsAbleToInstallMissingContent.NoMissingContent;

        public IsAbleToInstallMissingContent IsAbleToInstallMissingContentState {
            get { return _isAbleToInstallMissingContentState; }
            set {
                if (Equals(value, _isAbleToInstallMissingContentState)) return;
                _isAbleToInstallMissingContentState = value;
                OnPropertyChanged();
                _installMissingContentCommand?.RaiseCanExecuteChanged();
            }
        }

        private static IEnumerable<string> GetKeys([CanBeNull] JToken token) {
            var obj = token as JObject;
            if (obj == null) yield break;

            foreach (var p in obj) {
                yield return p.Key;
            }
        }

        private ServerStatus? UpdateMissingContentExtended([NotNull] ICollection<string> errors, bool alreadyMissingSomething) {
            var mref = _missingContentReferences;
            if (mref == null) {
                IsAbleToInstallMissingContentState = alreadyMissingSomething ?
                        IsAbleToInstallMissingContent.NotAbleTo :
                        IsAbleToInstallMissingContent.NoMissingContent;
                return null;
            }

            var missingSomething = false;

            var cars = mref["cars"] as JObject;
            if (cars != null) {
                var missingSkins = new List<string>();

                foreach (var carPair in cars) {
                    var skins = carPair.Value["skins"] as JObject;
                    if (skins != null) {
                        var carId = carPair.Key;
                        var car = CarsManager.Instance.GetById(carId);
                        if (car == null) continue;

                        foreach (var skinPair in skins) {
                            if (car.SkinsManager.GetWrapperById(skinPair.Key) == null) {
                                missingSkins.Add($"“{skinPair.Key}” ({car.DisplayName})");
                            }
                        }
                    }
                }

                if (missingSkins.Any()) {
                    errors.Add(string.Format(
                            missingSkins.Count == 1 ? ToolsStrings.Online_Server_CarSkinIsMissing : ToolsStrings.Online_Server_CarSkinsAreMissing,
                            missingSkins.JoinToReadableString()));
                    missingSomething = true;
                }
            }

            var missingWeatherIds = GetKeys(mref["weather"]).Where(x => WeatherManager.Instance.GetWrapperById(x) == null).ToList();
            if (missingWeatherIds.Any()) {
                errors.Add(string.Format(ToolsStrings.Online_Server_WeatherIsMissing,
                        missingWeatherIds.Select(x => $"“{x}”").JoinToReadableString()));
                missingSomething = true;
            }

            if (!missingSomething && !alreadyMissingSomething) {
                IsAbleToInstallMissingContentState = IsAbleToInstallMissingContent.NoMissingContent;
            } else {
                var allCarsAvailable = true;
                var trackBaseAvailable = true;
                var trackAvailable = true;

                var missingCarsIds = Cars?.Where(x => !x.CarExists).Select(x => x.Id).ToList();
                if (missingCarsIds?.Count > 0) {
                    var availableCarsIds = GetKeys(mref["cars"]).ToList();
                    var missingButAvailable = missingCarsIds.Where(availableCarsIds.Contains).ToList();
                    allCarsAvailable = missingCarsIds.Count == missingButAvailable.Count;
                }

                if (TrackBaseId != null && TracksManager.Instance.GetWrapperById(TrackBaseId) == null) {
                    trackBaseAvailable = mref["trackBase"] != null;
                }

                if (Track == null) {
                    trackAvailable = mref["track"] != null;
                }

                IsAbleToInstallMissingContentState = allCarsAvailable && trackBaseAvailable && trackAvailable
                        ? IsAbleToInstallMissingContent.AllOfIt : IsAbleToInstallMissingContent.Partially;
            }

            return missingSomething ? ServerStatus.MissingContent : (ServerStatus?)null;
        }
        #endregion
    }
}
