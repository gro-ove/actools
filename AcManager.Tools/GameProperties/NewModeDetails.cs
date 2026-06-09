using System;
using AcManager.Tools.Data;
using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class NewModeDetails : Game.RaceIniProperties {
        private readonly string _modeId;
        private readonly string _forceWeather;
        private readonly long? _forceTime;
        private readonly Tuple<double, double> _forceWind;

        public NewModeDetails(string modeId, string forceWeather, long? forceTime, Tuple<double, double> forceWind) {
            _modeId = modeId;
            _forceWeather = forceWeather;
            _forceTime = forceTime;
            _forceWind = forceWind;
        }

        public override void Set(IniFile file) {
            file["RACE"].Set("__CM_CUSTOM_MODE", _modeId);
            if (_forceWeather != null) {
                file["LIGHTING"].Set("__CM_WEATHER_TYPE", Enum.TryParse<WeatherType>(_forceWeather, true, out var ret) 
                        ? (int)ret : (int)WeatherType.Clear);
                file["LIGHTING"].Set("__CM_WEATHER_CONTROLLER", @"base");
                file["LIGHTING"].Remove("__CM_WEATHER_HUMIDITY");
                file["LIGHTING"].Remove("__CM_WEATHER_PRESSURE");
            }
            if (_forceTime.HasValue) {
                file["LIGHTING"].Set("__CM_DATE", _forceTime.Value);
                file["LIGHTING"].Set("__CM_DATE_USE_TIME", true);
            }
            if (_forceWind != null) {
                file["WIND"].Set("DIRECTION_DEG", _forceWind.Item1);
                file["WIND"].Set("SPEED_KMH_MIN", _forceWind.Item2);
                file["WIND"].Set("SPEED_KMH_MAX", _forceWind.Item2);
            }
        }
    }
}