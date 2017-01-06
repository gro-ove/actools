using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Pages.SelectionLists {
    public partial class TrackCountries_New {
        protected override Uri GetPageAddress(SelectCountry category) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}",
                $"enabled+&country:{Filter.Encode(category.DisplayName)}", category.DisplayName);
        }

        public TrackCountries_New() : base(TracksManager.Instance) {
            InitializeComponent();
        }

        protected override SelectCountry GetSelectedItem(IList<SelectCountry> list, TrackObject selected) {
            return list.FirstOrDefault(x => x.DisplayName == selected?.Country);
        }

        protected override void AddNewIfMissing(IList<SelectCountry> list, TrackObject obj) {
            if (obj.Country == null) return;

            for (var i = list.Count - 1; i >= 0; i--) {
                var item = list[i];
                if (item.DisplayName == obj.Country) {
                    IncreaseCounter(obj, item);
                    return;
                }
            }

            AddNewIfMissing(list, obj, new SelectCountry(obj.Country));
        }
    }
}
