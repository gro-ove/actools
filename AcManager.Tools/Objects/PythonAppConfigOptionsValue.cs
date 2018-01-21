using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigOptionsValue : PythonAppConfigValue {
        public IReadOnlyList<SettingEntry> Values { get; }

        public new SettingEntry Value {
            get => Values.GetByIdOrDefault(base.Value) ?? Values.FirstOrDefault();
            set => base.Value = value.Value;
        }

        public PythonAppConfigOptionsValue(IReadOnlyList<SettingEntry> values) {
            Values = values;
        }
    }
}