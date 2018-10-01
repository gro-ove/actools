using AcManager.Tools.Data;
using AcTools.DataFile;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class WeatherDetails : Game.RaceIniProperties {
        [CanBeNull]
        public WeatherDescription Description { get; }

        public WeatherDetails([CanBeNull] WeatherDescription description) {
            Description = description;
        }

        public override void Set(IniFile file) {
            if (Description != null) {
                file["LIGHTING"].Set("__CM_WEATHER_TYPE", (int)Description.Type);
                file["LIGHTING"].Set("__CM_WEATHER_TEMPERATURE", Description.Temperature);
                file["LIGHTING"].Set("__CM_WEATHER_HUMIDITY", Description.Humidity);
                file["LIGHTING"].Set("__CM_WEATHER_PRESSURE", Description.Pressure);
                file["LIGHTING"].Set("__CM_WEATHER_WINDSPEED", Description.WindSpeed);
                file["LIGHTING"].Set("__CM_WEATHER_WINDDIRECTION", Description.WindDirection);
            } else {
                file["LIGHTING"].Set("__CM_WEATHER_TYPE", -1);
                file["LIGHTING"].Remove("__CM_WEATHER_TEMPERATURE");
                file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
                file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
                file["LIGHTING"].Remove("__CM_WEATHER_WINDSPEED");
                file["LIGHTING"].Remove("__CM_WEATHER_WINDDIRECTION");
            }
        }
    }
}