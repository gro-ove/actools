using AcTools.Utils.Helpers;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigNumberValue : PythonAppConfigValue {
        public new double Value {
            get => FlexibleParser.TryParseDouble(base.Value) ?? 0d;
            set {
                if (Equals(value, Value)) return;
                base.Value = value.ToInvariantString();
            }
        }
    }
}