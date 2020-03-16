using System;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ServerWeatherEntry : NotifyPropertyChanged, IDraggable {
        private int _index;

        public int Index {
            get => _index;
            set => Apply(value, ref _index);
        }

        public ServerWeatherEntry() {
            WeatherId = WeatherManager.Instance.GetDefault()?.Id;
            BaseAmbientTemperature = 18d;
            BaseRoadTemperature = 24d;
            AmbientTemperatureVariation = 2d;
            RoadTemperatureVariation = 1d;
        }

        public ServerWeatherEntry(IniFileSection section) {
            WeatherId = section.GetNonEmpty("__CM_GRAPHICS") ?? section.GetNonEmpty("GRAPHICS");

            WeatherFxMode = section.ContainsKey(@"__CM_WFX_TYPE");
            WeatherFxType = section.GetEnum("__CM_WFX_TYPE", WeatherType.Clear);
            WeatherFxCustomStartTime = section.GetBool("__CM_WFX_USE_CUSTOM_TIME", true);
            WeatherFxStartTime = section.GetInt("__CM_WFX_TIME", 12 * 60 * 60);
            WeatherFxTimeMultiplier = section.GetDouble("__CM_WFX_TIME_MULT", 1.0);
            WeatherFxCustomStartDate = section.GetBool("__CM_WFX_USE_CUSTOM_DATE", false);

            var timezoneOffset = 0L; // (long)TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalSeconds;
            WeatherFxStartDate = (section.GetLong("__CM_WFX_DATE", DateTime.Now.ToUnixTimestamp()) + timezoneOffset).ToDateTime();

            BaseAmbientTemperature = section.GetDouble("BASE_TEMPERATURE_AMBIENT", 18d);
            BaseRoadTemperature = section.GetDouble("BASE_TEMPERATURE_ROAD", 6d) + BaseAmbientTemperature;
            AmbientTemperatureVariation = section.GetDouble("VARIATION_AMBIENT", 2d);
            RoadTemperatureVariation = section.GetDouble("VARIATION_ROAD", 1d);
            WindSpeedMin = section.GetDouble("WIND_BASE_SPEED_MIN", 0);
            WindSpeedMax = section.GetDouble("WIND_BASE_SPEED_MAX", 0);
            WindDirection = section.GetInt("WIND_BASE_DIRECTION", 0);
            WindDirectionVariation = section.GetInt("WIND_VARIATION_DIRECTION", 0);
        }

        public void SaveTo(IniFileSection section) {
            if (WeatherFxMode) {
                var timezoneOffset = 0L; // (long)TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalSeconds;
                section.Set("GRAPHICS", $@"{WeatherId}_type={(int)WeatherFxType}" + new[] {
                    WeatherFxCustomStartTime ? $@"_time={WeatherFxStartTime}" : "",
                    WeatherFxCustomStartTime && Math.Abs(WeatherFxTimeMultiplier - 1.0) > 0.001 ? $@"_mult={WeatherFxTimeMultiplier.Round(0.01)}" : "",
                    WeatherFxCustomStartDate ? $@"_start={WeatherFxStartDate.ToUniversalTime().Date.ToUnixTimestamp() - timezoneOffset}" : ""
                }.JoinToString(""));
                section.Set("__CM_GRAPHICS", WeatherId);
                section.Set("__CM_WFX_TYPE", WeatherFxType);
                section.Set("__CM_WFX_USE_CUSTOM_TIME", WeatherFxCustomStartTime);
                section.Set("__CM_WFX_TIME", WeatherFxStartTime);
                section.Set("__CM_WFX_TIME_MULT", WeatherFxTimeMultiplier);
                section.Set("__CM_WFX_USE_CUSTOM_DATE", WeatherFxCustomStartDate);
                section.Set("__CM_WFX_DATE", WeatherFxStartDate.ToUnixTimestamp() - timezoneOffset);
            } else {
                section.Set("GRAPHICS", WeatherId);
                section.Remove(@"__CM_GRAPHICS");
                section.Remove(@"__CM_WFX_TYPE");
                section.Remove(@"__CM_WFX_USE_CUSTOM_TIME");
                section.Remove(@"__CM_WFX_TIME");
                section.Remove(@"__CM_WFX_TIME_MULT");
                section.Remove(@"__CM_WFX_USE_CUSTOM_DATE");
                section.Remove(@"__CM_WFX_DATE");
            }

            section.Set("BASE_TEMPERATURE_AMBIENT", BaseAmbientTemperature);
            section.Set("BASE_TEMPERATURE_ROAD", BaseRoadTemperature - BaseAmbientTemperature);
            section.Set("VARIATION_AMBIENT", AmbientTemperatureVariation);
            section.Set("VARIATION_ROAD", RoadTemperatureVariation);
            section.Set("WIND_BASE_SPEED_MIN", WindSpeedMin);
            section.Set("WIND_BASE_SPEED_MAX", WindSpeedMax);
            section.Set("WIND_BASE_DIRECTION", WindDirection);
            section.Set("WIND_VARIATION_DIRECTION", WindDirectionVariation);
        }

        private string _weatherId;

        [CanBeNull]
        public string WeatherId {
            get => _weatherId;
            set {
                if (Equals(value, _weatherId)) return;
                _weatherSet = false;
                _weather = null;
                _weatherId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Weather));
                OnPropertyChanged(nameof(RecommendedRoadTemperature));
            }
        }

        public static WeatherType[] WeatherTypes { get; } = EnumExtension.GetValues<WeatherType>();

        private bool _weatherFxMode;

        public bool WeatherFxMode {
            get => _weatherFxMode;
            set => Apply(value, ref _weatherFxMode);
        }

        private WeatherType _weatherFxType = WeatherType.Clear;

        public WeatherType WeatherFxType {
            get => _weatherFxType;
            set => Apply(value, ref _weatherFxType);
        }

        private bool _weatherFxCustomStartTime;

        public bool WeatherFxCustomStartTime {
            get => _weatherFxCustomStartTime;
            set => Apply(value, ref _weatherFxCustomStartTime);
        }

        private int _weatherFxStartTime = 12 * 60 * 60;

        public int WeatherFxStartTime {
            get => _weatherFxStartTime;
            set => Apply(value, ref _weatherFxStartTime);
        }

        private double _weatherFxTimeMultiplier = 1d;

        public double WeatherFxTimeMultiplier {
            get => _weatherFxTimeMultiplier;
            set => Apply(value, ref _weatherFxTimeMultiplier);
        }

        private bool _weatherFxCustomStartDate;

        public bool WeatherFxCustomStartDate {
            get => _weatherFxCustomStartDate;
            set => Apply(value, ref _weatherFxCustomStartDate);
        }

        private DateTime _weatherFxStartDate = DateTime.Now;

        public DateTime WeatherFxStartDate {
            get => _weatherFxStartDate;
            set => Apply(value.ToUnixTimestamp() < TimeSpan.FromHours(12).TotalSeconds ? DateTime.Now :  value, ref _weatherFxStartDate);
        }

        private bool _weatherSet;
        private WeatherObject _weather;

        [CanBeNull]
        public WeatherObject Weather {
            get {
                if (!_weatherSet) {
                    _weatherSet = true;
                    _weather = WeatherId == null ? null : WeatherManager.Instance.GetById(WeatherId);
                }
                return _weather;
            }
            set => WeatherId = value?.Id;
        }

        private double _baseAmbientTemperature;

        public double BaseAmbientTemperature {
            get => _baseAmbientTemperature;
            set {
                value = value.Clamp(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum).Round(0.1);
                if (Equals(value, _baseAmbientTemperature)) return;
                _baseAmbientTemperature = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecommendedRoadTemperature));
            }
        }

        private double _baseRoadTemperature;

        public double BaseRoadTemperature {
            get => _baseRoadTemperature;
            set {
                value = value.Clamp(CommonAcConsts.RoadTemperatureMinimum, CommonAcConsts.RoadTemperatureMaximum).Round(0.1);
                if (Equals(value, _baseRoadTemperature)) return;
                _baseRoadTemperature = value;
                OnPropertyChanged();
            }
        }

        private double _ambientTemperatureVariation;

        public double AmbientTemperatureVariation {
            get => _ambientTemperatureVariation;
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _ambientTemperatureVariation)) return;
                _ambientTemperatureVariation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AmbientTemperatureVariationHalf));
            }
        }

        public double AmbientTemperatureVariationHalf {
            get => _ambientTemperatureVariation / 2d;
            set => AmbientTemperatureVariation = value * 2d;
        }

        private double _roadTemperatureVariation;

        public double RoadTemperatureVariation {
            get => _roadTemperatureVariation;
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _roadTemperatureVariation)) return;
                _roadTemperatureVariation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RoadTemperatureVariationHalf));
            }
        }

        public double RoadTemperatureVariationHalf {
            get => _roadTemperatureVariation / 2d;
            set => RoadTemperatureVariation = value * 2d;
        }

        private int _time;

        internal int Time {
            set {
                _time = value;
                OnPropertyChanged(nameof(RecommendedRoadTemperature));
            }
        }

        public double RecommendedRoadTemperature =>
                Game.ConditionProperties.GetRoadTemperature(_time, BaseAmbientTemperature, Weather?.TemperatureCoefficient ?? 1d);

        private double _windSpeedMin;

        public double WindSpeedMin {
            get => _windSpeedMin;
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _windSpeedMin)) return;
                _windSpeedMin = value;
                OnPropertyChanged();
            }
        }

        private double _windSpeedMax;

        public double WindSpeedMax {
            get => _windSpeedMax;
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _windSpeedMax)) return;
                _windSpeedMax = value;
                OnPropertyChanged();
            }
        }

        private int _windDirection;

        public int WindDirection {
            get => _windDirection;
            set {
                value = (value % 360 + 360) % 360;
                if (Equals(value, _windDirection)) return;
                _windDirection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindDirectionFlipped));
                OnPropertyChanged(nameof(DisplayWindDirection));
            }
        }

        public string DisplayWindDirection => _windDirection.ToDisplayWindDirection();

        public int WindDirectionFlipped {
            get => (_windDirection + 180) % 360;
            set => WindDirection = (value - 180) % 360;
        }

        private int _windDirectionVariation;

        public int WindDirectionVariation {
            get => _windDirectionVariation;
            set {
                value = value.Clamp(0, 180);
                if (Equals(value, _windDirectionVariation)) return;
                _windDirectionVariation = value;
                OnPropertyChanged();
            }
        }

        private bool _deleted;

        public bool Deleted {
            get => _deleted;
            set => Apply(value, ref _deleted);
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            Deleted = true;
        }));

        public const string DraggableFormat = "Data-ServerWeatherEntry";

        string IDraggable.DraggableFormat => DraggableFormat;
    }
}