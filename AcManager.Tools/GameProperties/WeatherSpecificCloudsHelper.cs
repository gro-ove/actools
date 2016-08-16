using System;
using System.IO;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class WeatherSpecificCloudsHelper : WeatherSpecificHelperBase {
        private static string Destination => Path.Combine(AcRootDirectory.Instance.RequireValue, @"content", @"texture", @"clouds");

        private static string Backup => Path.Combine(AcRootDirectory.Instance.RequireValue, @"content", @"texture", @"clouds_backup_cm");

        public static void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            try {
                var backup = Backup;
                if (Directory.Exists(backup)) {
                    var destination = Destination;

                    if (Directory.Exists(destination)) {
                        Directory.Delete(destination, true);
                    }

                    Directory.Move(backup, destination);
                }
            } catch (Exception e) {
                Logging.Warning("[WeatherSpecificCloudsHelper] Revert(): " + e);
            }
        }

        private string _destination, _backup, _replacement;

        protected override bool SetOverride(WeatherObject weather) {
            _replacement = Path.Combine(weather.Location, "clouds");
            if (!Directory.Exists(_replacement)) return false;

            _destination = Destination;
            _backup = Backup;

            if (Directory.Exists(_destination)) {
                if (Directory.Exists(_backup)) {
                    Directory.Move(_backup, FileUtils.EnsureUnique(_backup));
                }

                Directory.Move(_destination, _backup);
            }

            try {
                FileUtils.CopyRecursiveHardlink(_replacement, _destination);
            } catch (Exception e) {
                // this exception should be catched here so original clouds folder still
                // will be restored even when copying a new one has been failed
                Logging.Warning("[WeatherSpecificCloudsHelper] SetOverride(): " + e);
            }

            return true;
        }

        protected override void DisposeOverride() {
            if (Directory.Exists(_backup)) {
                if (Directory.Exists(_destination)) {
                    Directory.Delete(_destination, true);
                }

                Directory.Move(_backup, _destination);
            }
        }
    }
}