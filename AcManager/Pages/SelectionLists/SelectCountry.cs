using JetBrains.Annotations;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;

namespace AcManager.Pages.SelectionLists {
    public sealed class SelectCountry : SelectCategoryBase {
        [CanBeNull]
        public string CountryId { get; }

        public SelectCountry([NotNull] string name) : base(name) {
            CountryId = DataProvider.Instance.CountryToIds.GetValueOrDefault(AcStringValues.CountryFromTag(name) ?? "");
        }

        public override bool IsSameAs(SelectCategoryBase category) {
            return (category as SelectCountry)?.CountryId == CountryId;
        }
        
        internal override string Serialize() {
            return CountryId + @"|" + DisplayName;
        }

        private SelectCountry([NotNull] string name, [NotNull] string countryId) : base(name) {
            CountryId = countryId;
        }

        [CanBeNull]
        internal static SelectCountry Deserialize(string data) {
            var s = data.Split(new [] { '|' }, 2);
            if (s.Length != 2) return null;

            return new SelectCountry(s[1], s[0]);
        }
    }
}