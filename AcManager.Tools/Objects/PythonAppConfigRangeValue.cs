using System;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigRangeValue : PythonAppConfigValue {
        public double Minimum { get; }

        public double Maximum { get; }

        public double Tick { get; }

        public double RoundTo { get; }

        [CanBeNull]
        public string Postfix { get; }

        public new double Value {
            get => FlexibleParser.TryParseDouble(base.Value) ?? (Minimum + Maximum) / 2;
            set {
                value = value.Clamp(Minimum, Maximum).Round(RoundTo);
                if (Equals(value, Value)) return;
                base.Value = value.ToInvariantString();
            }
        }

        public PythonAppConfigRangeValue(double minimum, double maximum, [CanBeNull] string postfix) {
            Minimum = minimum;
            Maximum = maximum;
            Postfix = postfix;
            Tick = (maximum - minimum) / 10d;
            RoundTo = Math.Min(Math.Pow(10, Math.Round(Math.Log10(Maximum - Minimum) - 2)), 1d);
        }
    }
}