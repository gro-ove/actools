using System;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcTools.DataFile;

namespace AcManager.Tools.Objects {
    public class WeatherObject : AcIniObject {
        public WeatherObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        private WeatherDescription.WeatherType? _weatherType;

        public WeatherDescription.WeatherType? WeatherType {
            get { return _weatherType; }
            set { 
                if (Equals(_weatherType, value)) return;

                _weatherType = value; 
                OnPropertyChanged(nameof(WeatherType));
                OnPropertyChanged(nameof(WeatherTypeInformation));
                Changed = true;
            }
        }

        private double _temperatureCoefficient;

        public double TemperatureCoefficient {
            get { return _temperatureCoefficient; }
            set { 
                if (Equals(_temperatureCoefficient, value)) return;

                _temperatureCoefficient = value;
                OnPropertyChanged(nameof(TemperatureCoefficient));
                Changed = true;
            }
        }

        public string WeatherTypeInformation => _weatherType.HasValue ? WeatherDescription.GetWeatherTypeName(_weatherType.Value) : "None";

        private static WeatherDescription.WeatherType? TryToDetectWeatherTypeById(string id) {
            var lid = id.ToLower();

            if (lid.Contains("mid_clear")) return WeatherDescription.WeatherType.FewClouds;
            if (lid.Contains("light_clouds")) return WeatherDescription.WeatherType.ScatteredClouds;
            if (lid.Contains("heavy_clouds")) return WeatherDescription.WeatherType.OvercastClouds;

            if (lid.Contains("clouds")) return WeatherDescription.WeatherType.BrokenClouds;
            if (lid.Contains("clear")) return WeatherDescription.WeatherType.Clear;
            
            if (lid.Contains("light_fog")) return WeatherDescription.WeatherType.Mist;
            if (lid.Contains("fog")) return WeatherDescription.WeatherType.Fog;

            return null;
        }

        public string ColorCurvesIniFilename => Path.Combine(Location, "colorCurves.ini");

        public override string IniFilename => Path.Combine(Location, "weather.ini");

        protected override void LoadData(IniFile ini) {
            Name = ini["LAUNCHER"].Get("NAME");
            TemperatureCoefficient = ini["LAUNCHER"].GetDouble("TEMPERATURE_COEFF", 0d);

            try {
                WeatherType = ini["__LAUNCHER_CM"].GetEnumNullable<WeatherDescription.WeatherType>("WEATHER_TYPE");
            } catch (Exception) {
                WeatherType = null;
            }

            if (!WeatherType.HasValue) {
                WeatherType = TryToDetectWeatherTypeById(Id);
            }
        }

        public override void SaveData(IniFile ini) {
            ini["LAUNCHER"].Set("NAME", Name);
            ini["LAUNCHER"].Set("TEMPERATURE_COEFF", TemperatureCoefficient);
            ini["__LAUNCHER_CM"].Set("WEATHER_TYPE", WeatherType);
        }
    }
}
