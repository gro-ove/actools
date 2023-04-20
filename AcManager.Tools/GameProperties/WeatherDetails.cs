using AcManager.Tools.Data;
using AcTools.DataFile;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class WeatherDetails : Game.RaceIniProperties {
        [CanBeNull]
        private readonly WeatherTypeWrapped _weatherType;

        private readonly string _weatherDefaultControllerId;

        [CanBeNull]
        public WeatherDescription Description { get; }

        public WeatherDetails([CanBeNull] WeatherDescription description, [CanBeNull] WeatherTypeWrapped weatherType, string weatherDefaultControllerId) {
            _weatherType = weatherType;
            _weatherDefaultControllerId = weatherDefaultControllerId;
            Description = description;
        }

        public override void Set(IniFile file) {
            if (Description != null) {
                file["LIGHTING"].Set("__CM_WEATHER_TYPE", (int)Description.Type);
                file["LIGHTING"].Set("__CM_WEATHER_CONTROLLER", _weatherDefaultControllerId);
                file["LIGHTING"].Set("__CM_WEATHER_HUMIDITY", Description.Humidity);
                file["LIGHTING"].Set("__CM_WEATHER_PRESSURE", Description.Pressure);
            } else {
                file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
                file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
                if (_weatherType == null) {
                    file["LIGHTING"].Set("__CM_WEATHER_TYPE", -1);
                    file["LIGHTING"].Set("__CM_WEATHER_CONTROLLER", _weatherDefaultControllerId);
                } else if (_weatherType.ControllerId != null) {
                    _weatherType.PublishSettings();
                    file["LIGHTING"].Set("__CM_WEATHER_TYPE", (int)WeatherType.Clear);
                    file["LIGHTING"].Set("__CM_WEATHER_CONTROLLER", _weatherType.ControllerId);
                    file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
                    file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
                } else {
                    file["LIGHTING"].Set("__CM_WEATHER_TYPE", (int)_weatherType.TypeOpt);
                    file["LIGHTING"].Set("__CM_WEATHER_CONTROLLER", _weatherDefaultControllerId);
                    file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
                    file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
                }
            }
        }
    }
}