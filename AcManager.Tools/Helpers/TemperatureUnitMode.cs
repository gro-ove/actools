using System.ComponentModel;

namespace AcManager.Tools.Helpers {
    public enum TemperatureUnitMode {
        [Description("Celsius")]
        Celsius,

        [Description("Fahrenheit")]
        Fahrenheit,

        [Description("Celsius and Fahrenheit")]
        Both
    }
}