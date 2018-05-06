using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class WeatherSpecificTyreSmokeHelper : WeatherSpecificHelperBase {
        private static readonly IWeatherSpecificReplacement[] Replacements = {
            new WeatherSpecificFileReplacementBase(@"tyre_smoke.ini", @"system\cfg\tyre_smoke.ini"),
            new WeatherSpecificFileReplacementBase(@"tyre_smoke_grass.ini", @"system\cfg\tyre_smoke_grass.ini")
        };

        public static void Revert() {
            foreach (var replacement in Replacements) {
                replacement.Revert();
            }
        }

        protected override bool SetOverride(WeatherObject weather, IniFile file) {
            return Replacements.Aggregate(false, (current, replacement) => replacement.Apply(weather) || current);
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}