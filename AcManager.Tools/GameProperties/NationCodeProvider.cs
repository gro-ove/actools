using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Processes;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.GameProperties {
    public class NationCodeProvider : INationCodeProvider {
        public static readonly NationCodeProvider Instance = new NationCodeProvider();

        private NationCodeProvider() { }

        public string GetNationCode(string country) {
            return string.IsNullOrWhiteSpace(country) ? null :
                    DataProvider.Instance.CountryToKunosIds.GetValueOrDefault(AcStringValues.CountryFromTag(country) ?? "");
        }
    }
}