using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigOptionsValue : PythonAppConfigValue {
        public IReadOnlyList<object> Values { get; }

        public new SettingEntry Value {
            get => Values.OfType<SettingEntry>().GetByIdOrDefault(base.Value) ?? Values.OfType<SettingEntry>().FirstOrDefault();
            set => base.Value = value.Value;
        }

        public PythonAppConfigOptionsValue(IReadOnlyList<object> values) {
            Values = values;
        }

        public override string DisplayValueString => Value?.DisplayName;
    }
}