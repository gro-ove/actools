using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class SidekickHelper {
        public static double OptionRangeThreshold = 0;

        public static readonly string SidekickAppId = @"Sidekick";
        private static readonly Regex SidekickNameRegex = new Regex(@"\W+", RegexOptions.Compiled);

        [CanBeNull]
        private static Lut GetThermalLut(DataWrapper data, string sectionName) {
            var thermalSection = data.GetIniFile("tyres.ini")[$@"THERMAL_{sectionName}"];
            var thermalFile = thermalSection.GetNonEmpty("PERFORMANCE_CURVE");
            if (thermalFile == null) return null;

            var thermalLut = data.GetLutFile(thermalFile);
            return thermalLut.IsEmptyOrDamaged() ? null : thermalLut.Values;
        }

        [CanBeNull]
        private static Tuple<double, double> CombineRanges([CanBeNull] Tuple<double, double> a, [CanBeNull] Tuple<double, double> b) {
            return a == null ? b : b == null ? a :
                    new Tuple<double, double>(Math.Min(a.Item1, b.Item1), Math.Max(a.Item2, b.Item2));
        }

        [CanBeNull]
        public static Tuple<double, double> GetOptimalRange([CanBeNull] Lut lut) {
            if (lut == null) return null;

            lut.UpdateBoundingBox();

            var threshold = Math.Min(lut.MaxY, 1d) * (1d - OptionRangeThreshold);
            double? fromX = null, toX = null;

            lut.ForEach((x, y) => {
                if (y >= threshold) {
                    if (!fromX.HasValue) {
                        fromX = x;
                    }

                    toX = x;
                }
            });

            return fromX.HasValue && toX.HasValue ? new Tuple<double, double>(fromX ?? 0d, toX ?? 0d) : null;
        }

        private static bool SetRange(IniFileSection section, string minKey, string maxKey, [CanBeNull] Tuple<double, double> range) {
            if (range == null) return false;

            var min = Math.Min(range.Item1, range.Item2);
            var max = Math.Max(range.Item1, range.Item2);
            var changed = false;

            if (!Equals(section.GetDouble(maxKey, 0d), max)) {
                section.Set(maxKey, max);
                changed = true;
            }

            if (!Equals(section.GetDouble(minKey, 0d), min)) {
                section.Set(minKey, min);
                changed = true;
            }

            return changed;
        }

        private static void Prepare(string filename) {
            var directory = Path.GetDirectoryName(filename);
            if (directory != null && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
        }

        private static void UpdateSidekickCompounds(string appDirectory, DataWrapper wrapper, string carId,
                bool separateFiles, bool updateIfChanged) {
            var filename = Path.Combine(appDirectory, @"compounds", separateFiles ? $@"{carId}.ini" : @"compounds.ini");
            Prepare(filename);

            var sidekickCompounds = new IniFile(filename, IniFileMode.ValuesWithSemicolons);
            var tyres = wrapper.GetIniFile("tyres.ini");
            var changed = false;

            foreach (var sectionNameFront in tyres.GetExistingSectionNames(@"FRONT", -1)) {
                var sectionNameRear = sectionNameFront.Replace(@"FRONT", @"REAR");

                var sectionFront = tyres[sectionNameFront];
                var sectionRear = tyres[sectionNameRear];

                var idealPressureFront = sectionFront.GetDouble("PRESSURE_IDEAL", 0d);
                var idealPressureRear = sectionRear.GetDouble("PRESSURE_IDEAL", 0d);

                var thermalRange = GetOptimalRange(GetThermalLut(wrapper, sectionNameFront));
                if (sectionFront.GetNonEmpty("PERFORMANCE_CURVE") != sectionRear.GetNonEmpty("PERFORMANCE_CURVE")) {
                    var thermalRangeRear = GetOptimalRange(GetThermalLut(wrapper, sectionNameRear));
                    thermalRange = CombineRanges(thermalRange, thermalRangeRear);
                }

                var name = SidekickNameRegex.Replace($@"{sectionFront.GetNonEmpty("NAME")}_{sectionFront.GetNonEmpty("SHORT_NAME")}", "_").TrimEnd('_')
                                            .ToLowerInvariant();
                var sidekickSectionName = $@"{carId}_{name}";
                if (sidekickCompounds.ContainsKey(sidekickSectionName) && !updateIfChanged) continue;

                var sidekickSection = sidekickCompounds[sidekickSectionName];
                if (idealPressureFront > 0d && !Equals(sidekickSection.GetDouble("IDEAL_PRESSURE_F", 0d), idealPressureFront)) {
                    sidekickSection.Set("IDEAL_PRESSURE_F", idealPressureFront);
                    changed = true;
                }

                if (idealPressureRear > 0d && !Equals(sidekickSection.GetDouble("IDEAL_PRESSURE_R", 0d), idealPressureRear)) {
                    sidekickSection.Set("IDEAL_PRESSURE_R", idealPressureRear);
                    changed = true;
                }

                changed |= SetRange(sidekickSection, @"MIN_OPTIMAL_TEMP", @"MAX_OPTIMAL_TEMP", thermalRange);
            }

            if (changed) {
                Logging.Debug("Compounds database updated");
                sidekickCompounds.Save();
            }
        }

        private static void UpdateSidekickBrakes(string appDirectory, DataWrapper wrapper, string carId, bool separateFiles, bool updateIfChanged) {
            var brakes = wrapper.GetIniFile("brakes.ini");

            var frontCurve = brakes["TEMPS_FRONT"].GetLut("PERF_CURVE");
            var rearCurve = brakes["TEMPS_REAR"].GetLut("PERF_CURVE");

            if ((frontCurve == null || frontCurve.Count < 2) &&
                    (rearCurve == null || rearCurve.Count < 2)) {
                return;
            }

            var filename = Path.Combine(appDirectory, @"brakes", separateFiles ? $@"{carId}.ini" : @"brakes.ini");
            Prepare(filename);

            var sidekickBrakes = new IniFile(filename, IniFileMode.ValuesWithSemicolons);
            if (sidekickBrakes.ContainsKey(carId) && !updateIfChanged) return;

            var section = sidekickBrakes[carId];
            if (SetRange(section, @"MIN_OPTIMAL_TEMP_F", @"MAX_OPTIMAL_TEMP_F", GetOptimalRange(frontCurve)) |
                    SetRange(section, @"MIN_OPTIMAL_TEMP_R", @"MAX_OPTIMAL_TEMP_R", GetOptimalRange(rearCurve))) {
                Logging.Debug("Brakes database updated");
                sidekickBrakes.Save();
            }
        }

        public static void UpdateSidekickDatabase([NotNull] CarObject car, bool? separateFiles = null) {
            try {
                var directory = Path.Combine(AcPaths.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), SidekickAppId);
                if (!Directory.Exists(directory)) return;

                if (!AcSettingsHolder.Python.IsActivated(SidekickAppId)) {
                    Logging.Write("App is not active");
                    return;
                }

                if (car.AcdData?.IsEmpty != false) {
                    Logging.Write("Data is damaged");
                    return;
                }

                var kunosCar = car.Author == AcCommonObject.AuthorKunos;
                var separateFilesActual = separateFiles ?? !kunosCar;
                var updateIfChanged = kunosCar ? SettingsHolder.Drive.SidekickUpdateExistingKunos :
                        SettingsHolder.Drive.SidekickUpdateExistingMods;

                UpdateSidekickCompounds(directory, car.AcdData, car.Id, separateFilesActual, updateIfChanged);
                UpdateSidekickBrakes(directory, car.AcdData, car.Id, separateFilesActual, updateIfChanged);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public static void UpdateSidekickDatabase([NotNull] string carId, bool? separateFiles = null) {
            var car = CarsManager.Instance.GetById(carId);
            if (car == null) {
                Logging.Write($"Car “{carId}” not found");
                return;
            }

            UpdateSidekickDatabase(car, separateFiles);
        }

        #region Odometer
        public static readonly string OdometerDataFileName = Path.Combine("odometers", "odometers.ini");

        static SidekickHelper() {
            PlayerStatsManager.Instance.NewSessionAdded += OnNewSessionAdded;
        }

        private static void OnNewSessionAdded(object sender, PlayerStatsManager.SessionStatsEventArgs sessionStatsEventArgs) {
            if (!SettingsHolder.Drive.SidekickOdometerExportValues) return;
            if (sessionStatsEventArgs.Stats.CarId != null) {
                OdometerImport(sessionStatsEventArgs.Stats.CarId);
            }
        }

        /// <summary>
        /// Meters!
        /// </summary>
        private static IEnumerable<Tuple<string, double>> LoadDistances() {
            var filename = Path.Combine(AcPaths.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), SidekickAppId, OdometerDataFileName);
            if (!File.Exists(filename)) yield break;
            foreach (var v in new IniFile(filename)) {
                var carId = v.Key;
                var value = v.Value.GetDouble("odometer", 0d);
                if (value > 0) {
                    yield return Tuple.Create(carId, value);
                }
            }
        }

        private static void OdometerImportIfNeeded(string carId, double appDistance) {
            // Sanity check
            if (appDistance / 1e3 > 1e9) return;

            var cmDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(carId);
            if (cmDistance >= appDistance - 100) return;

            PlayerStatsManager.Instance.SetDistanceDrivenByCar(carId, appDistance);
            (CarsManager.Instance.GetWrapperById(carId)?.Value as CarObject)?.RaiseTotalDrivenDistanceChanged();
            Logging.Debug($"Driven distance for {carId} updated: CM had {cmDistance / 1e3:F1} km, Sidekick app has {appDistance / 1e3:F1} km, which is more.");
        }

        public static void OdometerImportAll() {
            foreach (var v in LoadDistances()) {
                OdometerImportIfNeeded(v.Item1, v.Item2);
            }
        }

        public static void OdometerImport([NotNull] string carId) {
            if (carId == null) throw new ArgumentNullException(nameof(carId));

            var cmDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(carId);
            if (cmDistance <= 0d) return;

            var filename = Path.Combine(AcPaths.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), SidekickAppId, OdometerDataFileName);
            if (!File.Exists(filename)) return;

            OdometerImportIfNeeded(carId, new IniFile(filename)[carId].GetDouble("odometer", 0d) * 1e3);
        }

        public static void OdometerExport([NotNull] string carId) {
            if (carId == null) throw new ArgumentNullException(nameof(carId));

            var cmDistance = PlayerStatsManager.Instance.GetDistanceDrivenByCar(carId);
            if (!(cmDistance > 0d)) return;

            var filename = Path.Combine(AcPaths.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), SidekickAppId, OdometerDataFileName);
            if (!File.Exists(filename)) return;

            var ini = new IniFile(filename);
            var section = ini[carId];
            var appDistance = section.GetDouble("odometer", 0d) * 1e3;
            if (cmDistance <= appDistance + 100) return;

            section.Set("odometer", cmDistance / 1e3);

            if (!section.ContainsKey("odometer.originalvalue")) {
                section.Set("odometer.originalvalue", appDistance / 1e3);
            }

            ini.Save();
            Logging.Debug($"Driven distance for {carId} updated: app had {appDistance / 1e3:F1} km, CM has {cmDistance / 1e3:F1} km, which is more.");
        }
        #endregion
    }
}