using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public class WeatherDescription {
        public WeatherType Type { get; }

        public double Temperature { get; }

        public double WindSpeed { get; }

        public double WindDirection { get; }

        /// <summary>
        /// Units: percentage.
        /// </summary>
        public double Humidity  { get; }

        /// <summary>
        /// Units: hPa, aka 100 Pa.
        /// </summary>
        public double Pressure  { get; }

        [CanBeNull]
        public string Icon { get; }

        public string Description { get; }

        public string FullDescription => $"{Description.Substring(0, 1).ToUpper() + Description.Substring(1)} ({Temperature:F1} °C)";

        public bool HasIcon => Icon != null;

        public WeatherDescription(WeatherType type, double temperature, string description, double windSpeed, double windDirection, double humidity,
                double pressure, string icon = null) {
            Type = type;
            Temperature = temperature;
            Description = description;
            WindSpeed = windSpeed;
            WindDirection = windDirection;
            Humidity = humidity;
            Pressure = pressure;
            Icon = icon;
        }
    }
}
