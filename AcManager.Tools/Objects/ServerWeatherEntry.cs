using AcManager.Tools.Managers;
using AcTools;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ServerWeatherEntry : NotifyPropertyChanged, IDraggable {
        private int _index;

        public int Index {
            get { return _index; }
            set {
                if (Equals(value, _index)) return;
                _index = value;
                OnPropertyChanged();
            }
        }

        public ServerWeatherEntry() {
            WeatherId = WeatherManager.Instance.GetDefault()?.Id;
            BaseAmbientTemperature = 18d;
            BaseRoadTemperature = 6d;
            AmbientTemperatureVariation = 2d;
            RoadTemperatureVariation = 1d;
        }

        public ServerWeatherEntry(IniFileSection section) {
            WeatherId = section.GetNonEmpty("GRAPHICS");
            BaseAmbientTemperature = section.GetDouble("BASE_TEMPERATURE_AMBIENT", 18d);
            BaseRoadTemperature = section.GetDouble("BASE_TEMPERATURE_ROAD", 6d);
            AmbientTemperatureVariation = section.GetDouble("VARIATION_AMBIENT", 2d);
            RoadTemperatureVariation = section.GetDouble("VARIATION_ROAD", 1d);
        }

        public void SaveTo(IniFileSection section) {
            section.Set("GRAPHICS", WeatherId);
            section.Set("BASE_TEMPERATURE_AMBIENT", BaseAmbientTemperature);
            section.Set("BASE_TEMPERATURE_ROAD", BaseRoadTemperature);
            section.Set("VARIATION_AMBIENT", AmbientTemperatureVariation);
            section.Set("VARIATION_ROAD", RoadTemperatureVariation);
        }

        private string _weatherId;

        [CanBeNull]
        public string WeatherId {
            get { return _weatherId; }
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
            set { WeatherId = value?.Id; }
        }

        private double _baseAmbientTemperature;

        public double BaseAmbientTemperature {
            get { return _baseAmbientTemperature; }
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
            get { return _baseRoadTemperature; }
            set {
                value = value.Clamp(CommonAcConsts.RoadTemperatureMinimum, CommonAcConsts.RoadTemperatureMaximum).Round(0.1);
                if (Equals(value, _baseRoadTemperature)) return;
                _baseRoadTemperature = value;
                OnPropertyChanged();
            }
        }

        private double _ambientTemperatureVariation;

        public double AmbientTemperatureVariation {
            get { return _ambientTemperatureVariation; }
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _ambientTemperatureVariation)) return;
                _ambientTemperatureVariation = value;
                OnPropertyChanged();
            }
        }

        private double _roadTemperatureVariation;

        public double RoadTemperatureVariation {
            get { return _roadTemperatureVariation; }
            set {
                value = value.Clamp(0, 100).Round(0.1);
                if (Equals(value, _roadTemperatureVariation)) return;
                _roadTemperatureVariation = value;
                OnPropertyChanged();
            }
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

        private bool _deleted;

        public bool Deleted {
            get { return _deleted; }
            set {
                if (Equals(value, _deleted)) return;
                _deleted = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            Deleted = true;
        }));

        public const string DraggableFormat = "Data-ServerWeatherEntry";

        string IDraggable.DraggableFormat => DraggableFormat;
    }
}