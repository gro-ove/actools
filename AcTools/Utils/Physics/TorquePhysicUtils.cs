using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcTools.Utils.Physics {
    public static class TorquePhysicUtils {
        public static void ConsiderTurbo(IReadOnlyList<TurboDescription> turbo, Lut torqueValues) {
            torqueValues.TransformSelf(x => ConsiderTurbo(turbo, x.X, x.Y));
        }

        public static double ConsiderTurbo(IReadOnlyList<TurboDescription> turbo, double rpm, double torque) {
            var multipler = 0d;
            for (var i = 0; i < turbo.Count; i++) {
                multipler += turbo[i].CalculateMultipler(rpm);
            }

            return torque * (1.0 + multipler);
        }

        private const double TorqueRpmToBhpMultipler = 1.0 / (9.5488 * 745.7);

        [Pure]
        public static double TorqueToPower(double torque, double rpm) {
            return rpm * torque * TorqueRpmToBhpMultipler;
        }

        [Pure]
        public static double PowerToTorque(double power, double rpm) {
            return Equals(rpm, 0d) ? 0d : power / rpm / TorqueRpmToBhpMultipler;
        }

        private static Lut Result(Lut v) {
            v = v.Optimize();
            v.UpdateBoundingBox();
            return v;
        }

        [NotNull]
        public static Lut TorqueToPower(Lut torque, int detalization = 100) {
            return Result(torque.Select((x, y) => new LutPoint(x, TorqueToPower(y, x)), detalization));
        }

        [NotNull]
        public static Lut PowerToTorque(Lut torque, int detalization = 100) {
            return Result(torque.Select((x, y) => new LutPoint(x, PowerToTorque(y, x)), detalization));
        }

        [CanBeNull]
        private static IReadOnlyList<TurboControllerDescription> ReadControllers(IniFile file) {
            if (file.IsEmptyOrDamaged()) return null;
            return file.GetSections("CONTROLLER").Select(TurboControllerDescription.FromIniSection).ToList();
        }

        [NotNull]
        private static IReadOnlyList<TurboDescription> ReadTurbos(IniFile file) {
            if (file.IsEmptyOrDamaged()) return new TurboDescription[0];
            return file.GetSections("TURBO").Select(TurboDescription.FromIniSection).ToList();
        }

        [NotNull]
        public static Lut LoadCarTorque([NotNull] IDataWrapper data, bool considerLimiter = true, int detalization = 100) {
            /* read torque curve and engine params */
            var engine = data.GetIniFile("engine.ini");
            if (engine.IsEmptyOrDamaged()) throw new FileNotFoundException("Can’t load engine.ini", "data/engine.ini");

            /* prepare turbos and read controllers */
            var turbos = ReadTurbos(engine);
            for (var i = 0; i < turbos.Count; i++) {
                turbos[i].Controllers = ReadControllers(data.GetIniFile($"ctrl_turbo{i}.ini"));
            }

            /* prepare torque curve and limits */
            var torque = engine["HEADER"].GetLut("POWER_CURVE", "power.lut");
            torque.UpdateBoundingBox();

            if (torque.Count < 2) {
                throw new Exception("Can’t use torque curve, it should have at least two points");
            }

            var limit = considerLimiter && engine.ContainsKey("ENGINE_DATA") ? engine["ENGINE_DATA"].GetDouble("LIMITER", torque.MaxX) : torque.MaxX;
            var startFrom = considerLimiter ? 0d : torque.MinX;

            /* build smoothed line */
            var result = new Lut();

            var previousTorquePoint = 0;
            var previousRpm = 0d;
            for (var i = 0; i <= detalization; i++) {
                var rpm = detalization == 0 ? limit : (limit - startFrom) * i / detalization + startFrom;

                for (var j = previousTorquePoint; j < torque.Count; j++) {
                    var p = torque[j];

                    if (p.X > rpm) {
                        previousTorquePoint = j > 0 ? j - 1 : 0;
                        break;
                    }

                    if ((i == 0 || p.X > previousRpm) && p.X < rpm && p.X >= 0) {
                        result.Add(new LutPoint(p.X, ConsiderTurbo(turbos, p.X, p.Y)));
                    }
                }

                var baseTorque = torque.InterpolateLinear(rpm);
                result.Add(new LutPoint(rpm, ConsiderTurbo(turbos, rpm, baseTorque)));
                previousRpm = rpm;
            }

            return Result(result);
        }

        [Obsolete]
        public static Lut LoadCarTorque(string carDir) {
            return LoadCarTorque(DataWrapper.FromCarDirectory(carDir));
        }
    }
}
