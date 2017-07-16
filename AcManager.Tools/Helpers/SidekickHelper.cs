using System;
using System.IO;
using System.Text.RegularExpressions;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class SidekickHelper {
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
            if (a == null) return b;
            if (b == null) return a;
            return new Tuple<double, double>(Math.Min(a.Item1, b.Item1), Math.Max(a.Item2, b.Item2));
        }

        [CanBeNull]
        private static Tuple<double, double> GetOptimalRange([CanBeNull] Lut lut) {
            if (lut == null) return null;

            double? fromX = null, toX = null;
            for (var i = 0; i < lut.Count; i++) {
                var point = lut[i];
                if (point.Y >= 0.999d) {
                    if (!fromX.HasValue) {
                        fromX = point.X;
                    }
                    toX = point.X;
                }
            }

            return fromX.HasValue ? new Tuple<double, double>(fromX.Value, toX.Value) : null;
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
                var directory = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), SidekickAppId);
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
    }
}