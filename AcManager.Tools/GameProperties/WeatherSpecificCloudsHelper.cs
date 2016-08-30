using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    internal interface IWeatherSpecificReplacement {
        bool Apply(WeatherObject weather);

        void Revert();
    }

    internal class WeatherSpecificDirectoryReplacement : IWeatherSpecificReplacement {
        public string RelativeSource { get; }

        public string RelativeDestination { get; }

        protected string RelativeBackup { get; }

        internal WeatherSpecificDirectoryReplacement(string relativeRelativeSource, string relativeDestination) {
            RelativeSource = relativeRelativeSource;
            RelativeDestination = relativeDestination;
            RelativeBackup = RelativeDestination + @"_backup_cm";
        }

        public bool Apply(WeatherObject weather) {
            if (AcRootDirectory.Instance.Value == null) return false;

            var source = Path.Combine(weather.Location, RelativeSource);
            if (!Directory.Exists(source)) return false;

            var destination = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeDestination);
            var backup = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeBackup);

            if (Directory.Exists(destination)) {
                if (Directory.Exists(backup)) {
                    Directory.Move(backup, FileUtils.EnsureUnique(backup));
                }

                Logging.Debug($"{destination} → {backup}");
                Directory.Move(destination, backup);
            }


            try {
                Logging.Debug($"{source} → {destination}");
                FileUtils.CopyRecursiveHardlink(source, destination);
            } catch (Exception e) {
                // this exception should be catched here so original clouds folder still
                // will be restored even when copying a new one has been failed
                NonfatalError.Notify("Can’t replace weather-specific directory", e);
            }

            return true;
        }

        public void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            var destination = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeDestination);
            var backup = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeBackup);

            try {
                if (Directory.Exists(backup)) {
                    if (Directory.Exists(destination)) {
                        Directory.Delete(destination, true);
                    }

                    Directory.Move(backup, destination);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t restore original directory after replacing it with a weather-specific one", e);
            }
        }
    }

    internal class WeatherSpecificFileReplacement : IWeatherSpecificReplacement {
        public string RelativeSource { get; }

        public string RelativeDestination { get; }

        protected string RelativeBackup { get; }

        internal WeatherSpecificFileReplacement(string relativeRelativeSource, string relativeDestination) {
            RelativeSource = relativeRelativeSource;
            RelativeDestination = relativeDestination;
            RelativeBackup = RelativeDestination + @"_backup_cm";
        }

        public bool Apply(WeatherObject weather) {
            if (AcRootDirectory.Instance.Value == null) return false;

            var source = Path.Combine(weather.Location, RelativeSource);
            if (!File.Exists(source)) return false;

            var destination = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeDestination);
            var backup = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeBackup);
            if (File.Exists(destination)) {
                if (File.Exists(backup)) {
                    File.Move(backup, FileUtils.EnsureUnique(backup));
                }

                Logging.Debug($"{destination} → {backup}");
                File.Move(destination, backup);
            }

            try {
                Logging.Debug($"{source} → {destination}");
                FileUtils.Hardlink(source, destination);
            } catch (Exception e) {
                // this exception should be catched here so original clouds folder still
                // will be restored even when copying a new one has been failed
                NonfatalError.Notify("Can’t replace weather-specific files", e);
            }

            return true;
        }

        public void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            try {
                var destination = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeDestination);
                var backup = Path.Combine(AcRootDirectory.Instance.RequireValue, RelativeBackup);

                if (File.Exists(backup)) {
                    if (File.Exists(destination)) {
                        File.Delete(destination);
                    }

                    File.Move(backup, destination);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t restore original files after replacing them with weather-specific ones", e);
            }
        }
    }

    public class WeatherSpecificCloudsHelper : WeatherSpecificHelperBase {
        private static readonly IWeatherSpecificReplacement[] Replacements = {
            new WeatherSpecificDirectoryReplacement(@"clouds", @"content\texture\clouds"),
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
            new WeatherSpecificFileReplacement(@"tyre_smoke.ini", @"system\cfg\tyre_smoke.ini"), 
            new WeatherSpecificFileReplacement(@"tyre_smoke_grass.ini", @"system\cfg\tyre_smoke_grass.ini")
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