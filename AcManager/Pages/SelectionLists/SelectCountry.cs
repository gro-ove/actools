using System;
using JetBrains.Annotations;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.SelectionLists {
    public sealed class SelectCountry : Displayable, IComparable, IComparable<SelectCountry> {
        [CanBeNull]
        public string CountryId { get; }

        public SelectCountry([NotNull] string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            DisplayName = name;
            CountryId = DataProvider.Instance.CountryToIds.GetValueOrDefault(AcStringValues.CountryFromTag(name) ?? "");
        }

        public override string ToString() => DisplayName;

        int IComparable.CompareTo(object obj) => string.Compare(DisplayName, obj.ToString(), StringComparison.InvariantCulture);

        int IComparable<SelectCountry>.CompareTo(SelectCountry other) => string.Compare(DisplayName, other.DisplayName, StringComparison.InvariantCulture);
    }
}