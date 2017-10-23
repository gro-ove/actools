using System;
using System.Collections.Generic;
using System.IO;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class StereoOdometerHelper {
        public static readonly string OdometerAppId = @"stereo_odometer";
        public static readonly string DataFileName = @"odometers.txt";

        static StereoOdometerHelper() {
            PlayerStatsManager.Instance.NewSessionAdded += OnNewSessionAdded;
        }

        private static void OnNewSessionAdded(object sender, PlayerStatsManager.SessionStatsEventArgs sessionStatsEventArgs) {
            if (!SettingsHolder.Drive.StereoOdometerExportValues) return;
            if (sessionStatsEventArgs.Stats.CarId != null) {
                Import(sessionStatsEventArgs.Stats.CarId);
            }
        }

        /// <summary>
        /// Meters!
        /// </summary>
        private static IEnumerable<Tuple<string, double>> LoadDistances() {
            var filename = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), OdometerAppId, DataFileName);
            if (!File.Exists(filename)) yield break;

            var section = new IniFile(filename)["Cars"];
            foreach (var v in section) {
                var carId = v.Key;
                var value = v.Value.AsDouble();
                if (value > 0) {
                    yield return Tuple.Create(carId, value);
                }
            }
        }

        private static void ImportIfNeeded(string carId, double appDistance) {
            var cmDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(carId);
            if (cmDistance >= appDistance - 100) return;

            PlayerStatsManager.Instance.SetDistanceDrivenByCar(carId, appDistance);
            (CarsManager.Instance.GetWrapperById(carId)?.Value as CarObject)?.RaiseTotalDrivenDistanceChanged();
            Logging.Debug($"Driven distance for {carId} updated: CM had {cmDistance / 1e3:F1} km, app has {appDistance / 1e3:F1} km, which is more.");
        }

        public static void ImportAll() {
            foreach (var v in LoadDistances()) {
                ImportIfNeeded(v.Item1, v.Item2);
            }
        }

        public static void Import([NotNull] string carId) {
            if (carId == null) throw new ArgumentNullException(nameof(carId));

            var cmDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(carId);
            if (cmDistance <= 0d) return;

            var filename = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), OdometerAppId, DataFileName);
            if (!File.Exists(filename)) return;

            var ini = new IniFile(filename);
            ImportIfNeeded(carId, ini["Cars"].GetDouble(carId, 0d) * ini["Adjustments"].GetDouble(carId, 1d));
        }

        public static void Export([NotNull] string carId) {
            if (carId == null) throw new ArgumentNullException(nameof(carId));

            var cmDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(carId);
            if (!(cmDistance > 0d)) return;

            var filename = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), OdometerAppId, DataFileName);
            if (!File.Exists(filename)) return;

            var ini = new IniFile(filename);
            var cars = ini["Cars"];
            var multiplier = ini["Adjustments"].GetDouble(carId, 1d);
            var appDistance = cars.GetDouble(carId, 0d) * multiplier;
            if (cmDistance <= appDistance + 100) return;

            cars.Set(carId, cmDistance / multiplier);
            if (!cars.ContainsKey(carId + ".originalvalue")) {
                cars.Set(carId + ".originalvalue", appDistance / multiplier);
            }

            ini.Save();
            Logging.Debug($"Driven distance for {carId} updated: app had {appDistance / 1e3:F1} km, CM has {cmDistance / 1e3:F1} km, which is more.");
        }
    }
}