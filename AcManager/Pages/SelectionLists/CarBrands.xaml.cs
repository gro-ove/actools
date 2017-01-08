using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Pages.SelectionLists {
    public partial class CarBrands {
        public CarBrands() : base(CarsManager.Instance, true) {
            InitializeComponent();
        }

        protected override void AddNewIfMissing(IList<SelectCarBrand> list, CarObject obj) {
            var value = obj.Brand;
            if (value == null) return;

            for (var i = list.Count - 1; i >= 0; i--) {
                var item = list[i];
                if (item.DisplayName == value) {
                    IncreaseCounter(obj, item);
                    return;
                }
            }

            AddNewIfMissing(list, obj, new SelectCarBrand(value, obj.BrandBadge));
        }

        protected override SelectCarBrand GetSelectedItem(IList<SelectCarBrand> list, CarObject selected) {
            var value = selected?.Brand;
            if (value != null) {
                for (var i = list.Count - 1; i >= 0; i--) {
                    var x = list[i];
                    if (x.DisplayName == value) return x;
                }
            }

            return null;
        }

        protected override bool OnObjectPropertyChanged(CarObject obj, PropertyChangedEventArgs e) {
            return e.PropertyName == nameof(obj.Brand);
        }

        protected override Uri GetPageAddress(SelectCarBrand category) {
            return SelectCarDialog.BrandUri(category.DisplayName);
        }

        protected override SelectCarBrand LoadFromCache(string serialized) {
            return SelectCarBrand.Deserialize(serialized);
        }
    }
}
