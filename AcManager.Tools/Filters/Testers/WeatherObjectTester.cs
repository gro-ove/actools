using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class WeatherObjectTester : ITester<WeatherObject> {
        public static readonly WeatherObjectTester Instance = new WeatherObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "lights":
                case "carlights":
                    return nameof(WeatherObject.ForceCarLights);

                case "temperature":
                case "temperaturecoeff":
                case "temperaturecoefficient":
                    return nameof(WeatherObject.TemperatureCoefficient);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcCommonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(WeatherObject obj, string key, ITestEntry value) {
            switch (key) {
                case "lights":
                case "carlights":
                    return value.Test(obj.ForceCarLights);

                case "temperature":
                case "temperaturecoeff":
                case "temperaturecoefficient":
                    return value.Test(obj.TemperatureCoefficient);
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }
    }
}