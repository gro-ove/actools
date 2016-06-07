using System;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcTools.DataFile;

namespace AcManager.Tools.Objects {
    public class WeatherObject : AcIniObject {
        public WeatherObject(IFileAcManager manager, string fileName, bool enabled)
                : base(manager, fileName, enabled) {}

        private WeatherType? _type;

        public WeatherType? Type {
            get { return _type; }
            set { 
                if (Equals(_type, value)) return;
                _type = value; 
                OnPropertyChanged();
                Changed = true;
            }
        }

        private double _temperatureCoefficient;

        public double TemperatureCoefficient {
            get { return _temperatureCoefficient; }
            set { 
                if (Equals(_temperatureCoefficient, value)) return;
                _temperatureCoefficient = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private bool _forceCarLights;

        public bool ForceCarLights {
            get { return _forceCarLights; }
            set {
                if (Equals(value, _forceCarLights)) return;
                _forceCarLights = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private static WeatherType? TryToDetectWeatherTypeById(string id) {
            var lid = id.ToLower();

            if (lid.Contains("mid_clear")) return WeatherType.FewClouds;
            if (lid.Contains("light_clouds")) return WeatherType.ScatteredClouds;
            if (lid.Contains("heavy_clouds")) return WeatherType.OvercastClouds;

            if (lid.Contains("clouds")) return WeatherType.BrokenClouds;
            if (lid.Contains("clear")) return WeatherType.Clear;
            
            if (lid.Contains("light_fog")) return WeatherType.Mist;
            if (lid.Contains("fog")) return WeatherType.Fog;

            return null;
        }

        public string ColorCurvesIniFilename => Path.Combine(Location, "colorCurves.ini");

        public override string IniFilename => Path.Combine(Location, "weather.ini");

        protected override void LoadData(IniFile ini) {
            Name = ini["LAUNCHER"].Get("NAME");
            TemperatureCoefficient = ini["LAUNCHER"].GetDouble("TEMPERATURE_COEFF", 0d);
            ForceCarLights = ini["CAR_LIGHTS"].GetBool("FORCE_ON", false);

            try {
                Type = ini["__LAUNCHER_CM"].GetEnumNullable<WeatherType>("WEATHER_TYPE");
            } catch (Exception) {
                Type = null;
            }

            if (!Type.HasValue) {
                Type = TryToDetectWeatherTypeById(Id);
            }
        }

        public override void SaveData(IniFile ini) {
            ini["LAUNCHER"].Set("NAME", Name);
            ini["LAUNCHER"].Set("TEMPERATURE_COEFF", TemperatureCoefficient);
            ini["CAR_LIGHTS"].Set("FORCE_ON", ForceCarLights);
            ini["__LAUNCHER_CM"].Set("WEATHER_TYPE", Type);
        }
    }
}
