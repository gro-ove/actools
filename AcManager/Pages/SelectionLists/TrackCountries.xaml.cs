using System;
using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Pages.SelectionLists {
    public partial class TrackCountries {
        public TrackCountries() : base(TracksManager.Instance, true) {
            InitializeComponent();
        }

        protected override SelectCountry GetSelectedItem(IList<SelectCountry> list, TrackObject obj) {
            var value = obj?.Country;
            if (value != null) {
                for (var i = list.Count - 1; i >= 0; i--) {
                    var x = list[i];
                    if (x.DisplayName == value) return x;
                }
            }

            return null;
        }

        protected override SelectCountry LoadFromCache(string serialized) {
            return SelectCountry.Deserialize(serialized);
        }

        protected override void AddNewIfMissing(IList<SelectCountry> list, TrackObject obj) {
            var value = obj.Country;
            if (value == null) return;

            for (var i = list.Count - 1; i >= 0; i--) {
                var item = list[i];
                if (item.DisplayName == value) {
                    IncreaseCounter(obj, item);
                    return;
                }
            }

            AddNewIfMissing(list, obj, new SelectCountry(value));
        }

        protected override bool OnObjectPropertyChanged(TrackObject obj, PropertyChangedEventArgs e) {
            return e.PropertyName == nameof(obj.Country);
        }

        protected override Uri GetPageAddress(SelectCountry category) {
            return SelectTrackDialog.CountryUri(category.DisplayName);
        }
    }
}
