using System.IO;
using System.Linq;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Tools.GameProperties {
    internal interface IWeatherSpecificReplacement {
        bool Apply(WeatherObject weather);

        void Revert();
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

    public class CarSpecificControlsPresetHelper : CarSpecificHelperBase {
        private const string BackupPostfix = "_backup_cm_carspec";

        public static void Revert() {
            new TemporaryFileReplacement(null, AcSettingsHolder.Controls.Filename, BackupPostfix).Revert();
        }

        protected override bool SetOverride(CarObject car) {
            return car.SpecificControlsPreset && car.ControlsPresetFilename != null &&
                    new TemporaryFileReplacement(car.ControlsPresetFilename, AcSettingsHolder.Controls.Filename, BackupPostfix).Apply();
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}