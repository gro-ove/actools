using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcTools.Utils.Physics {
    public class TurboControllerDescription {
        public double UpLimit, DownLimit, Filter;
        public Combinator Combinator;
        public ControllerInput Input;

        [CanBeNull]
        public Lut Lut;

        [Pure, NotNull]
        public static TurboControllerDescription FromIniSection(IniFileSection controllerSection) {
            return new TurboControllerDescription {
                UpLimit = controllerSection.GetDouble("UP_LIMIT", double.NaN),
                DownLimit = controllerSection.GetDouble("DOWN_LIMIT", double.NaN),
                Filter = controllerSection.GetDouble("FILTER", 0.99d),
                Combinator = controllerSection.GetEnum("COMBINATOR", Combinator.None),
                Input = controllerSection.GetEnum("INPUT", ControllerInput.None),
                Lut = controllerSection.GetLut("LUT")
            };
        }

        [Pure]
        public double Process(double rpm, double boost) {
            double input;
            switch (Input) {
                case ControllerInput.Rpms:
                    input = rpm;
                    break;
                default:
                    input = 0d;
                    break;
            }
            
            var inputValue = Lut?.InterpolateLinear(input) ?? 0d;
            double resultValue;
            switch (Combinator) {
                case Combinator.Add:
                    resultValue = boost + inputValue;
                    break;
                case Combinator.Mult:
                    resultValue = boost * inputValue;
                    break;
                default:
                    return boost;
            }
            
            if (!double.IsNaN(UpLimit) && UpLimit < resultValue) {
                resultValue = UpLimit;
            }

            if (!double.IsNaN(DownLimit) && DownLimit > resultValue) {
                resultValue = DownLimit;
            }

            return resultValue;
        }
    }
}