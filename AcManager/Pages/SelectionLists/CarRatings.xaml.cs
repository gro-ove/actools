using System;
using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;

namespace AcManager.Pages.SelectionLists {
    public partial class CarRatings {
        public CarRatings() : base(CarsManager.Instance, true) {
            InitializeComponent();
        }

        private void AddNewRatingIfMissing(IList<SelectRating> list, CarObject obj) {
            if (obj == null) return;

            var value = (obj.Rating ?? 0d).FloorToInt();
            if (value == 0) return;

            for (var i = list.Count - 1; i >= 0; i--) {
                var item = list[i];
                if (item.Rating == value) {
                    IncreaseCounter(obj, item);
                    return;
                }
            }

            AddNewIfMissing(list, obj, new SelectRating(value));
        }

        private void AddNewFavouritesIfMissing(IList<SelectRating> list, CarObject obj) {
            var value = obj.IsFavourite;
            if (value == false) return;

            for (var i = list.Count - 1; i >= 0; i--) {
                var item = list[i];
                if (item.Rating == null) {
                    IncreaseCounter(obj, item);
                    return;
                }
            }

            AddNewIfMissing(list, obj, new SelectRating(null));
        }

        protected override void AddNewIfMissing(IList<SelectRating> list, CarObject obj) {
            AddNewRatingIfMissing(list, obj);
            AddNewFavouritesIfMissing(list, obj);
        }

        protected override SelectRating GetSelectedItem(IList<SelectRating> list, CarObject obj) {
            if (obj == null) return null;

            if (obj.IsFavourite) {
                for (var i = list.Count - 1; i >= 0; i--) {
                    var x = list[i];
                    if (x.Rating == null) return x;
                }
            }

            var value = (obj.Rating ?? 0d).FloorToInt();
            if (value != 0) {
                for (var i = list.Count - 1; i >= 0; i--) {
                    var x = list[i];
                    if (x.Rating == value) return x;
                }
            }

            return null;
        }

        protected override bool OnObjectPropertyChanged(CarObject obj, PropertyChangedEventArgs e) {
            return e.PropertyName == nameof(obj.IsFavourite) || e.PropertyName == nameof(obj.Rating);
        }

        protected override Uri GetPageAddress(SelectRating category) {
            return category.Rating == null ? SelectCarDialog.FavouritesUri() : SelectCarDialog.RatingUri(category.Rating.Value);
        }

        protected override SelectRating LoadFromCache(string serialized) {
            return SelectRating.Deserialize(serialized);
        }
    }
}
