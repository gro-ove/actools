using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class WeatherSpecificCloudsHelper : WeatherSpecificHelperBase {
        private static readonly IWeatherSpecificReplacement[] Replacements = {
            new WeatherSpecificDirectoryReplacementBase(@"clouds", @"content\texture\clouds"),
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