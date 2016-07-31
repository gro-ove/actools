using System;
using System.IO;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.SemiGui {
    public class WeatherSpecificCloudsHelper : Game.RaceIniProperties, IDisposable {
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
        private bool _requiredDisposal;

        public override void Set(IniFile file) {
            Logging.Write("[WeatherSpecificCloudsHelper] Set()");
            try {
                var weatherId = file["WEATHER"].Get("NAME");
                var weather = weatherId == null ? null : WeatherManager.Instance.GetById(weatherId);
                if (weather == null) return;

                _replacement = Path.Combine(weather.Location, "clouds");
                if (!Directory.Exists(_replacement)) return;

                _destination = Destination;
                _backup = Backup;

                if (Directory.Exists(_destination)) {
                    if (Directory.Exists(_backup)) {
                        Directory.Move(_backup, FileUtils.EnsureUnique(_backup));
                    }

                    Directory.Move(_destination, _backup);
                    _requiredDisposal = true;
                }
                
                FileUtils.CopyRecursiveHardlink(_replacement, _destination);
                _requiredDisposal = true;
            } catch (Exception e) {
                Logging.Warning("[WeatherSpecificCloudsHelper] Set(): " + e);
            }
        }

        public void Dispose() {
            Logging.Write("[WeatherSpecificCloudsHelper] Dispose()");
            if (!_requiredDisposal) return;

            try {
                if (Directory.Exists(_backup)) {
                    if (Directory.Exists(_destination)) {
                        Directory.Delete(_destination, true);
                    }

                    Directory.Move(_backup, _destination);
                }
            } catch (Exception e) {
                Logging.Warning("[WeatherSpecificCloudsHelper] Dispose(): " + e);
            }
        }
    }
}