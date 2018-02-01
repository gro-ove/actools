using System.Collections;
using System.Collections.Generic;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public class CarSkinComparer : IComparer, IComparer<AcItemWrapper>, IComparer<AcPlaceholderNew>, IComparer<CarSkinObject> {
        public static CarSkinComparer Comparer { get; } = new CarSkinComparer();

        private static int Compare(string x, string y) {
            return AlphanumComparatorFast.Compare(x, y);
        }

        public int Compare(object x, object y) {
            return Compare((x as AcItemWrapper)?.Value ?? x as AcPlaceholderNew, (y as AcItemWrapper)?.Value ?? y as AcPlaceholderNew);
        }

        public int Compare(AcItemWrapper x, AcItemWrapper y) {
            return Compare(x?.Value, y);
        }

        public int Compare(AcPlaceholderNew x, AcPlaceholderNew y) {
            if (x == null) return 1;
            if (y == null) return -1;
            if (x.Enabled != y.Enabled) return x.Enabled ? -1 : 1;
            switch (SettingsHolder.Content.CarSkinsSorting.SelectedValue) {
                case SettingsHolder.ContentSettings.SortName:
                    return Compare(x.DisplayName, y.DisplayName);
                case SettingsHolder.ContentSettings.SortSkinNumber:
                    var result = Compare((x as CarSkinObject)?.SkinNumber, (y as CarSkinObject)?.SkinNumber);
                    if (result == 0) {
                        goto default;
                    }
                    return result;
                default:
                    return Compare(x.Id, y.Id);
            }
        }

        public int Compare(CarSkinObject x, CarSkinObject y) {
            return Compare((AcPlaceholderNew)x, y);
        }
    }
}