using AcManager.Tools.Data;
using AcTools.DataFile;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class WeatherDetails : Game.RaceIniProperties {
        private readonly WeatherType? _weatherType;

        [CanBeNull]
        public WeatherDescription Description { get; }

        public WeatherDetails([CanBeNull] WeatherDescription description, WeatherType? weatherType) {
            _weatherType = weatherType;
            Description = description;
        }

        public override void Set(IniFile file) {
            if (Description != null) {
                file["LIGHTING"].Set("__CM_WEATHER_TYPE", (int)Description.Type);
                file["LIGHTING"].Set("__CM_WEATHER_HUMIDITY", Description.Humidity);
                file["LIGHTING"].Set("__CM_WEATHER_PRESSURE", Description.Pressure);
            } else {
                file["LIGHTING"].Set("__CM_WEATHER_TYPE", _weatherType.HasValue ? (int)_weatherType.Value : -1);
                file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
                file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
            }
        }
    }
}