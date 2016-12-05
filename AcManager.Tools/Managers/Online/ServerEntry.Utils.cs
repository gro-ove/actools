using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private static readonly Regex SpacesCollapseRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex SortingCheatsRegex = new Regex(@"^(?:AA+|[ !-]+|A(?![b-zB-Z0-9])+)+| ?-$", RegexOptions.Compiled);
        private static readonly Regex SimpleCleanUpRegex = new Regex(@"^AA+\s*", RegexOptions.Compiled);

        private static string CleanUp(string name, [CanBeNull] string oldName) {
            name = name.Trim();
            name = SpacesCollapseRegex.Replace(name, " ");
            if (SettingsHolder.Online.FixNames) {
                name = SortingCheatsRegex.Replace(name, "");
            } else if (oldName != null && SimpleCleanUpRegex.IsMatch(name) && !SimpleCleanUpRegex.IsMatch(oldName)) {
                name = SimpleCleanUpRegex.Replace(name, "");
            }
            return name;
        }
    }
}
