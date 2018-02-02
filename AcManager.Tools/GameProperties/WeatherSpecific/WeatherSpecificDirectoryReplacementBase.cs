using System.IO;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
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
}