using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigSection : ObservableCollection<PythonAppConfigValue> {
        public string Key { get; }

        public string DisplayName { get; }

        public PythonAppConfigSection(string appDirectory, KeyValuePair<string, IniFileSection> pair, [CanBeNull] IniFileSection values)
                : base(pair.Value.Select(x => PythonAppConfigValue.Create(appDirectory, x,
                        pair.Value.Commentaries?.GetValueOrDefault(x.Key), values?.GetValueOrDefault(x.Key), values != null))) {
            Key = pair.Key;

            var commentary = pair.Value.Commentary;
            DisplayName = commentary?.Trim() ?? PythonAppConfig.ConvertKeyToName(pair.Key);
        }
    }
}