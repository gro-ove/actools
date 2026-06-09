using System;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigRangeValue : PythonAppConfigValue {
        public PythonAppReferencedValue<double> Minimum { get; }
        public PythonAppReferencedValue<double> Maximum { get; }

        public bool ManualTick { get; set; }
        public double Tick { get; set; }

        public bool ManualRound { get; set; }
        public double RoundTo { get; set; }

        public double DisplayMultiplier { get; set; } = 1d;

        [CanBeNull]
        public string Postfix { get; set; }

        public new double Value {
            get => FlexibleParser.TryParseDouble(base.Value) ?? (Minimum.Value + Maximum.Value) / 2;
            set {
                // value = value.Clamp(Minimum.Value, Maximum.Value).Round(RoundTo);
                value = value.Round(RoundTo);
                if (Equals(value, Value)) return;
                base.Value = value.ToInvariantString();
            }
        }

        protected override void OnValueChanged() {
            base.OnValueChanged();
            OnPropertyChanged(nameof(DisplayValue));

            // Logging.Warning($"{Value}, Min={Minimum.Value}, Max={Maximum.Value}");

            if (Postfix?.EndsWith(@"(s)") == true) {
                DisplayPostix = Math.Abs(Value - 1) < 0.00001 ? Postfix.Replace(@"(s)", "") : Postfix.Replace(@"(s)", @"s");
            } else {
                DisplayPostix = Postfix;
            }
        }

        public override void UpdateReferenced(IPythonAppConfigValueProvider provider) {
            base.UpdateReferenced(provider);

            if (!Minimum.Refresh(provider) && !Maximum.Refresh(provider)) return;

            if (!ManualTick) {
                Tick = (Maximum.Value - Minimum.Value) / 10d;
                OnPropertyChanged(nameof(Tick));
            }

            if (!ManualRound) {
                RoundTo = Math.Min(Math.Pow(10, Math.Round(Math.Log10(Maximum.Value - Minimum.Value) - 2)), 1d);
                OnPropertyChanged(nameof(RoundTo));
            }

            Value = Value;
        }

        public double DisplayValue {
            get => Value * DisplayMultiplier;
            set => Value = value / DisplayMultiplier;
        }

        private string _displayPostix;

        public string DisplayPostix {
            get => _displayPostix;
            set => Apply(value, ref _displayPostix);
        }

        public override string DisplayValueString => DisplayValue + DisplayPostix;

        public PythonAppConfigRangeValue(PythonAppReferencedValue<double> minimum, PythonAppReferencedValue<double> maximum, [CanBeNull] string postfix) {
            Minimum = minimum;
            Maximum = maximum;
            Postfix = postfix;
            Tick = (maximum.Value - minimum.Value) / 10d;
            RoundTo = Math.Min(Math.Pow(10, Math.Round(Math.Log10(Maximum.Value - Minimum.Value) - 2)), 1d);
        }
    }
}