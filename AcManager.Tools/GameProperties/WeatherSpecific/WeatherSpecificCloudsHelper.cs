using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class WeatherSpecificCloudsHelper : WeatherSpecificHelperBase {
        private static readonly WeatherSpecificDirectoryReplacementBase Clouds
                = new WeatherSpecificDirectoryReplacementBase(@"clouds", @"content\texture\clouds");

        private static readonly IWeatherSpecificReplacement[] Replacements = { Clouds };

        public static void Revert() {
            foreach (var replacement in Replacements) {
                replacement.Revert();
            }
        }

        protected override bool SetOverride(WeatherObject weather, IniFile file) {
            var section = new IniFile(weather.IniFilename)["__CLOUDS_TEXTURES"];
            Clouds.SourceList = section.ContainsKey(@"LIST") ? section.GetStrings("LIST") : null;
            return Replacements.Aggregate(false, (current, replacement) => replacement.Apply(weather) || current);
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}