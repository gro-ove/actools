using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class RaceEssentialsHelper {
        public static readonly string RaceEssentialsAppId = @"RaceEssentials";
        private static readonly Regex RaceEssentialsNameRegex = new Regex(@"\W+", RegexOptions.Compiled);

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

        private class KeysSet {
            public Func<IniFileSection, string> NameGenerator { get; }

            public readonly string IdealPressureFront, IdealPressureRear,
                    MinOptimalTemperature, MaxOptimalTemperature,
                    MinOptimalBrakeTemperatureFront, MaxOptimalBrakeTemperatureFront,
                    MinOptimalBrakeTemperatureRear, MaxOptimalBrakeTemperatureRear;

            public KeysSet(string idealPressureFront, string idealPressureRear,
                    string minOptimalTemperature, string maxOptimalTemperature,
                    Func<IniFileSection, string> nameGenerator,
                    string minOptimalBrakeTemperatureFront = null, string maxOptimalBrakeTemperatureFront = null,
                    string minOptimalBrakeTemperatureRear = null, string maxOptimalBrakeTemperatureRear = null) {
                NameGenerator = nameGenerator;
                IdealPressureFront = idealPressureFront;
                IdealPressureRear = idealPressureRear;
                MinOptimalTemperature = minOptimalTemperature;
                MaxOptimalTemperature = maxOptimalTemperature;
                MinOptimalBrakeTemperatureFront = minOptimalBrakeTemperatureFront;
                MaxOptimalBrakeTemperatureFront = maxOptimalBrakeTemperatureFront;
                MinOptimalBrakeTemperatureRear = minOptimalBrakeTemperatureRear;
                MaxOptimalBrakeTemperatureRear = maxOptimalBrakeTemperatureRear;
            }
        }

        private static readonly KeysSet[] Keys = {
            new KeysSet("idealPressureF", "idealPressureR", "minOptimalTemp", "maxOptimalTemp", section => RaceEssentialsNameRegex
                    .Replace(section.GetNonEmpty("SHORT_NAME") ?? "", "_").TrimEnd('_').ToLowerInvariant(),
                    "minOptimalBrakeTempF", "maxOptimalBrakeTempF", "minOptimalBrakeTempR", "maxOptimalBrakeTempR"),
            new KeysSet("IDEAL_PRESSURE_F", "IDEAL_PRESSURE_R", "MIN_OPTIMAL_TEMP", "MAX_OPTIMAL_TEMP", section => RaceEssentialsNameRegex
                    .Replace($@"{section.GetNonEmpty("NAME")}_{section.GetNonEmpty("SHORT_NAME")}", "_").TrimEnd('_').ToLowerInvariant()),
        };

        private static void UpdateRaceEssentialsCompounds(string appDirectory, DataWrapper wrapper, string carId,
                bool separateFiles, bool updateIfChanged) {
            var mainIniKeys = new IniFile(Path.Combine(appDirectory, @"compounds", @"compounds.ini"), IniFileMode.ValuesWithSemicolons)
                    .SelectMany(x => x.Value.Keys).Distinct().ToList();
            Logging.Debug("Keys used: " + mainIniKeys.JoinToString(", "));
            var keys = Keys.FirstOrDefault(x => mainIniKeys.Contains(x.IdealPressureFront)) ?? Keys.First();

            // Loading file with compounds…
            var filename = Path.Combine(appDirectory, @"compounds", separateFiles ? $@"{carId}.ini" : @"compounds.ini");
            Prepare(filename);
            var compounds = new IniFile(filename, IniFileMode.ValuesWithSemicolons);
            var changed = false;

            // Preparing brakes
            var brakes = wrapper.GetIniFile("brakes.ini");
            var frontCurve = brakes["TEMPS_FRONT"].GetLut("PERF_CURVE");
            var rearCurve = brakes["TEMPS_REAR"].GetLut("PERF_CURVE");
            var frontBrakesRange = frontCurve == null || frontCurve.Count < 2 ? null : GetOptimalRange(frontCurve);
            var rearBrakesRange = rearCurve == null || rearCurve.Count < 2 ? null : GetOptimalRange(rearCurve);

            // Tyres
            var tyres = wrapper.GetIniFile("tyres.ini");

            // For each set…
            foreach (var sectionNameFront in tyres.GetExistingSectionNames(@"FRONT", -1)) {
                var sectionNameRear = sectionNameFront.Replace(@"FRONT", @"REAR");

                // Front and rear tyres
                var sectionFront = tyres[sectionNameFront];
                var sectionRear = tyres[sectionNameRear];

                // Ideal pressure
                var idealPressureFront = sectionFront.GetDouble("PRESSURE_IDEAL", 0d);
                var idealPressureRear = sectionRear.GetDouble("PRESSURE_IDEAL", 0d);

                // Find optimal termal range and, if different, combine front and rear ranges
                var thermalRange = GetOptimalRange(GetThermalLut(wrapper, sectionNameFront));
                if (sectionFront.GetNonEmpty("PERFORMANCE_CURVE") != sectionRear.GetNonEmpty("PERFORMANCE_CURVE")) {
                    var thermalRangeRear = GetOptimalRange(GetThermalLut(wrapper, sectionNameRear));
                    thermalRange = CombineRanges(thermalRange, thermalRangeRear);
                }

                // Finding name for resulting section
                var resultSectionName = $@"{carId}_{keys.NameGenerator(sectionFront)}";
                if (compounds.ContainsKey(resultSectionName) && !updateIfChanged) continue;

                // Applying ideal pressures…
                var resultSection = compounds[resultSectionName];
                if (idealPressureFront > 0d && !Equals(resultSection.GetDouble(keys.IdealPressureFront, 0d), idealPressureFront)) {
                    resultSection.Set(keys.IdealPressureFront, idealPressureFront);
                    changed = true;
                }

                if (idealPressureRear > 0d && !Equals(resultSection.GetDouble(keys.IdealPressureRear, 0d), idealPressureRear)) {
                    resultSection.Set(keys.IdealPressureRear, idealPressureRear);
                    changed = true;
                }

                // Temperature range…
                changed |= SetRange(resultSection, keys.MinOptimalTemperature, keys.MaxOptimalTemperature, thermalRange);

                // And brake temperatures if supported…
                if (frontBrakesRange != null && keys.MinOptimalBrakeTemperatureFront != null && keys.MaxOptimalBrakeTemperatureFront != null) {
                    changed |= SetRange(resultSection, keys.MinOptimalBrakeTemperatureFront, keys.MaxOptimalBrakeTemperatureFront, frontBrakesRange);
                }

                if (rearBrakesRange != null && keys.MinOptimalBrakeTemperatureRear != null && keys.MaxOptimalBrakeTemperatureRear != null) {
                    changed |= SetRange(resultSection, keys.MinOptimalBrakeTemperatureRear, keys.MaxOptimalBrakeTemperatureRear, rearBrakesRange);
                }
            }

            // Saving if changed
            if (changed) {
                Logging.Debug("Compounds database updated");
                compounds.Save();
            }
        }

        public static void UpdateRaceEssentialsDatabase([NotNull] CarObject car, bool? separateFiles = null) {
            try {
                var directory = Path.Combine(FileUtils.GetPythonAppsDirectory(AcRootDirectory.Instance.RequireValue), RaceEssentialsAppId);
                if (!Directory.Exists(directory)) return;

                if (!AcSettingsHolder.Python.IsActivated(RaceEssentialsAppId)) {
                    Logging.Write("App is not active");
                    return;
                }

                if (car.AcdData?.IsEmpty != false) {
                    Logging.Write("Data is damaged");
                    return;
                }

                var kunosCar = car.Author == AcCommonObject.AuthorKunos;
                var separateFilesActual = separateFiles ?? !kunosCar;
                var updateIfChanged = kunosCar ? SettingsHolder.Drive.RaceEssentialsUpdateExistingKunos :
                        SettingsHolder.Drive.RaceEssentialsUpdateExistingMods;

                UpdateRaceEssentialsCompounds(directory, car.AcdData, car.Id, separateFilesActual, updateIfChanged);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public static void UpdateRaceEssentialsDatabase([NotNull] string carId, bool? separateFiles = null) {
            var car = CarsManager.Instance.GetById(carId);
            if (car == null) {
                Logging.Write($"Car “{carId}” not found");
                return;
            }

            UpdateRaceEssentialsDatabase(car, separateFiles);
        }
    }
}