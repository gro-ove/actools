using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.NeuralTyres;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Tyres {
    public static class TyresExtension {
        [CanBeNull]
        public static TyresSet GetOriginalTyresSet([NotNull] this CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) return null;

            var front = TyresEntry.Create(car, @"__CM_FRONT_ORIGINAL", true);
            var rear = TyresEntry.Create(car, @"__CM_REAR_ORIGINAL", true);
            if (front != null && rear != null) {
                return new TyresSet(front, rear);
            } else {
                return null;
            }
        }

        [NotNull]
        public static IEnumerable<TyresSet> GetTyresSets([NotNull] this CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) return new TyresSet[0];

            var defaultSet = tyres["COMPOUND_DEFAULT"].GetInt("INDEX", 0);
            return TyresEntry.GetTyres(car).Where(x => x.Item1 != null && x.Item2 != null).Select((x, i) => new TyresSet(x.Item1, x.Item2) {
                DefaultSet = i == defaultSet
            });
        }

        public static void Save([NotNull] this TyresSet sets, int setsVersion, [NotNull] CarObject car,
                [CanBeNull] TyresEntry originalTyresFront, [CanBeNull] TyresEntry originalTyresRear, bool keepCurves = false) {
            Save(new[] { sets }, setsVersion, car, originalTyresFront, originalTyresRear, keepCurves);
        }

        public static void Save([NotNull] this IEnumerable<TyresSet> sets, int setsVersion, [NotNull] CarObject car,
                [CanBeNull] TyresEntry originalTyresFront, [CanBeNull] TyresEntry originalTyresRear, bool keepCurves = false) {
            try {
                var uniqueSets = sets.Distinct(TyresSet.TyresSetComparer).ToList();

                if (uniqueSets.Count == 0) {
                    throw new Exception("At least one set is required");
                }

                if (!uniqueSets.All(x => x.Front.Version == setsVersion && x.Rear.Version == setsVersion)) {
                    throw new Exception("Versions are different");
                }

                var data = car.AcdData;
                if (data == null) {
                    throw new Exception("Data is unreadable");
                }

                var tyresIni = data.GetIniFile("tyres.ini");
                tyresIni["HEADER"].Set("VERSION", setsVersion);

                var defaultIndex = uniqueSets.FindIndex(x => x.DefaultSet);
                tyresIni["COMPOUND_DEFAULT"].Set("INDEX", defaultIndex == -1 ? 0 : defaultIndex);

                if (originalTyresFront != null) {
                    tyresIni["__CM_FRONT_ORIGINAL"] = originalTyresFront.MainSection;
                    tyresIni["__CM_THERMAL_FRONT_ORIGINAL"] = originalTyresFront.ThermalSection;
                }

                if (originalTyresRear != null) {
                    tyresIni["__CM_REAR_ORIGINAL"] = originalTyresRear.MainSection;
                    tyresIni["__CM_THERMAL_REAR_ORIGINAL"] = originalTyresRear.ThermalSection;
                }

                SetTyres(true);
                SetTyres(false);

                void SetTyres(bool isRear) {
                    var key = isRear ? "REAR" : "FRONT";
                    var thermalKey = $"THERMAL_{key}";

                    var currentTyres = keepCurves ? tyresIni.GetSections(key, -1).ToList() : null;
                    var currentThermalTyres = keepCurves ? tyresIni.GetSections(thermalKey, -1).ToList() : null;

                    tyresIni.SetSections(key, -1, uniqueSets.Select((x, i) => {
                        var entry = isRear ? x.Rear : x.Front;
                        var curveName = currentTyres?.ElementAtOrDefault(i)?.GetNonEmpty("WEAR_CURVE");
                        if (!keepCurves || curveName == null) {
                            var curve = data.GetRawFile($@"__cm_tyre_wearcurve_{key.ToLowerInvariant()}_{i}.lut");
                            curve.Content = entry.WearCurveData ?? "";
                            curve.Save();
                            curveName = curve.Name;
                        }

                        return FixDigits(new IniFileSection(data, entry.MainSection) {
                            ["NAME"] = x.GetName(),
                            ["SHORT_NAME"] = x.GetShortName(),
                            ["WEAR_CURVE"] = curveName,
                            ["__CM_SOURCE_ID"] = entry.SourceCarId
                        });
                    }));

                    tyresIni.SetSections(thermalKey, -1, uniqueSets.Select((x, i) => {
                        var entry = isRear ? x.Rear : x.Front;
                        var curveName = currentThermalTyres?.ElementAtOrDefault(i)?.GetNonEmpty("PERFORMANCE_CURVE");
                        if (!keepCurves || curveName == null) {
                            var curve = data.GetRawFile($@"__cm_tyre_perfcurve_{key.ToLowerInvariant()}_{i}.lut");
                            curve.Content = entry.PerformanceCurveData ?? "";
                            curve.Save();
                            curveName = curve.Name;
                        }

                        return FixDigits(new IniFileSection(data, entry.ThermalSection) {
                            ["PERFORMANCE_CURVE"] = curveName
                        });
                    }));
                }

                IniFileSection FixDigits(IniFileSection result) {
                    foreach (var key in result.Keys.ToList()) {
                        var digits = GetValueDigits(key);
                        if (digits != null) {
                            result.Set(key, result.GetDouble(key, 0d), "F" + digits);
                        }
                    }
                    return result;
                }

                tyresIni.Save(true);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t save changes", e);
            }
        }

        public static int? GetValueDigits(string key) {
            switch (key) {
                case "DAMP":
                case "DCAMBER_1":
                case "FALLOFF_SPEED":
                case "FZ0":
                case "PRESSURE_IDEAL":
                case "PRESSURE_SPRING_GAIN":
                case "PRESSURE_STATIC":
                case "RATE":
                case "ROLLING_RESISTANCE_0":
                case "ROLLING_RESISTANCE_SLIP":
                    return 0;
                case "BRAKE_DX_MOD":
                case "COOL_FACTOR":
                case "CX_MULT":
                case "DCAMBER_0":
                case "FALLOFF_LEVEL":
                case "FRICTION_LIMIT_ANGLE":
                case "GRAIN_GAIN":
                case "PRESSURE_FLEX_GAIN":
                case "PRESSURE_RR_GAIN":
                case "ROLLING_K":
                case "XMU":
                    return 2;
                case "CAMBER_GAIN":
                case "DX_REF":
                case "DY_REF":
                    return 3;
                case "ANGULAR_INERTIA":
                case "BLISTER_GAMMA":
                case "DX0":
                case "DX1":
                case "DY0":
                case "DY1":
                case "GRAIN_GAMMA":
                case "FLEX_GAIN":
                case "LS_EXPY":
                case "LS_EXPX":
                case "PRESSURE_D_GAIN":
                case "RADIUS_ANGULAR_K":
                case "RIM_RADIUS":
                case "SURFACE_TRANSFER":
                case "WIDTH":
                    return 4;
                case "FRICTION_K":
                case "PATCH_TRANSFER":
                case "RADIUS":
                case "RELAXATION_LENGTH":
                case "SURFACE_ROLLING_K":
                    return 5;
                case "INTERNAL_CORE_TRANSFER":
                case "SPEED_SENSITIVITY":
                    return 6;
                case "CORE_TRANSFER":
                case "ROLLING_RESISTANCE_1":
                    return 7;
                case "FLEX":
                    return 8;
                default:
                    return 6;
            }
        }

        [NotNull]
        public static TyresEntry CreateTyresEntry([NotNull] this TyresMachine machine, double width, double radius, double profile) {
            var values = machine.Conjure(width, radius, profile);
            return TyresEntry.CreateFromNeural(values, null);
        }

        [NotNull]
        public static TyresEntry CreateTyresEntry([NotNull] this TyresMachine machine, [NotNull] TyresEntry original) {
            var values = machine.Conjure(original.Width, original.Radius, original.Radius - original.RimRadius);
            return TyresEntry.CreateFromNeural(values, original);
        }

        [NotNull]
        public static TyresSet CreateTyresSet([NotNull] this TyresMachine machine, [NotNull] TyresEntry frontOriginal, [NotNull] TyresEntry rearOriginal) {
            var front = CreateTyresEntry(machine, frontOriginal);
            var rear = CreateTyresEntry(machine, rearOriginal);
            return new TyresSet(front, rear);
        }

        [NotNull]
        public static TyresSet CreateTyresSet([NotNull] this TyresMachine machine, [NotNull] CarObject car) {
            var original = car.GetOriginalTyresSet() ?? car.GetTyresSets().First();
            var front = CreateTyresEntry(machine, original.Front);
            var rear = CreateTyresEntry(machine, original.Rear);
            return new TyresSet(front, rear);
        }
    }
}