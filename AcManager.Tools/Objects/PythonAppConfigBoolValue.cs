using System.ComponentModel;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigBoolValue : PythonAppConfigValue {
        private readonly string _trueValue;
        private readonly string _falseValue;

        public new bool Value {
            get => base.Value == _trueValue;
            set {
                if (Equals(value, Value)) return;
                base.Value = value ? _trueValue : _falseValue;
            }
        }

        public PythonAppConfigBoolValue([Localizable(false)] string trueValue = "True", [Localizable(false)] string falseValue = "False") {
            _trueValue = trueValue;
            _falseValue = falseValue;
        }

        public override string DisplayValueString => Value ? "yes" : "no";
    }
}