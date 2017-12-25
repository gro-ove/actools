using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Tools.GameProperties {
    internal interface IWeatherSpecificReplacement {
        bool Apply(WeatherObject weather);

        bool Revert();
    }

    internal class WeatherSpecificDirectoryReplacementBase : TemporaryDirectoryReplacementBase, IWeatherSpecificReplacement {
        public string RelativeSource { get; }

        internal WeatherSpecificDirectoryReplacementBase(string relativeSource, string relativeDestination) : base(relativeDestination) {
            RelativeSource = relativeSource;
        }

        public bool Apply(WeatherObject weather) {
            return Apply(Path.Combine(weather.Location, RelativeSource));
        }

        protected override string GetAbsolutePath(string relative) {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, relative);
        }
    }

    internal class WeatherSpecificFileReplacementBase : TemporaryFileReplacementBase, IWeatherSpecificReplacement {
        public string RelativeSource { get; }

        internal WeatherSpecificFileReplacementBase(string relativeRelativeSource, string relativeDestination) : base(relativeDestination) {
            RelativeSource = relativeRelativeSource;
        }

        public bool Apply(WeatherObject weather) {
            return Apply(Path.Combine(weather.Location, RelativeSource));
        }

        protected override string GetAbsolutePath(string relative) {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, relative);
        }
    }

    public class WeatherSpecificCloudsHelper : WeatherSpecificHelperBase {
        private static readonly IWeatherSpecificReplacement[] Replacements = {
            new WeatherSpecificDirectoryReplacementBase(@"clouds", @"content\texture\clouds"),
        };

        public static void Revert() {
            foreach (var replacement in Replacements) {
                replacement.Revert();
            }
        }

        protected override bool SetOverride(WeatherObject weather) {
            return Replacements.Aggregate(false, (current, replacement) => replacement.Apply(weather) || current);
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }

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

        protected override bool SetOverride(WeatherObject weather) {
            return Replacements.Aggregate(false, (current, replacement) => replacement.Apply(weather) || current);
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}