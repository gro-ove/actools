using System;

namespace AcTools.NeuralTyres.Data {
    public class AcNormalizationLimits : INormalizationLimits {
        public static readonly AcNormalizationLimits Default = new AcNormalizationLimits();

        public Tuple<double, double> GetLimits(string key) {
            switch (key) {
                case "WIDTH":
                case "RADIUS":
                case "RIM_RADIUS":
                    return Tuple.Create(0.0, double.PositiveInfinity);

                case "DX0":
                case "DX1":
                case "DY0":
                case "DY1":
                case "DCAMBER_1":
                    return null;

                case "COOL_FACTOR":
                case "DCAMBER_0":
                case "FALLOFF_SPEED":
                case "FZ0":
                case "PRESSURE_IDEAL":
                case "PRESSURE_SPRING_GAIN":
                case "PRESSURE_STATIC":
                case "RATE":
                case "ROLLING_RESISTANCE_0":
                case "ROLLING_RESISTANCE_SLIP":
                case "THERMAL@BLISTER_GAIN":
                case "THERMAL@BLISTER_GAMMA":
                case "THERMAL@GRAIN_GAMMA":
                    return Tuple.Create(0.0, double.PositiveInfinity);

                case "DAMP":
                    return Tuple.Create(0.0, 3000.0);

                case "ANGULAR_INERTIA":
                case "FALLOFF_LEVEL":
                    return Tuple.Create(0.0, 200.0);

                case "DX_REF":
                case "DY_REF":
                case "PRESSURE_FLEX_GAIN":
                case "PRESSURE_RR_GAIN":
                    return Tuple.Create(0.0, 100.0);

                case "FRICTION_LIMIT_ANGLE":
                    return Tuple.Create(0.0, 30.0);

                case "CX_MULT":
                case "LS_EXPX":
                case "LS_EXPY":
                    return Tuple.Create(0.0, 10.0);

                case "BRAKE_DX_MOD":
                case "CAMBER_GAIN":
                case "FLEX_GAIN":
                case "PRESSURE_D_GAIN":
                case "RADIUS_ANGULAR_K":
                case "RELAXATION_LENGTH":
                case "ROLLING_RESISTANCE_1":
                case "SPEED_SENSITIVITY":
                case "THERMAL@CORE_TRANSFER":
                case "THERMAL@FRICTION_K":
                case "THERMAL@GRAIN_GAIN":
                case "THERMAL@INTERNAL_CORE_TRANSFER":
                case "THERMAL@PATCH_TRANSFER":
                case "THERMAL@ROLLING_K":
                case "THERMAL@SURFACE_ROLLING_K":
                case "THERMAL@SURFACE_TRANSFER":
                case "XMU":
                    return Tuple.Create(0.0, 1.0);

                case "FLEX":
                    return Tuple.Create(0d, 0.1);

                default:
                    return null;


            }
        }
    }
}