using System.IO;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
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
}