using System;
using System.Collections.Generic;
using System.ComponentModel;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Pages.SelectionLists {
    public partial class CarDlcs {
        public CarDlcs() : base(CarsManager.Instance, true) {
            InitializeComponent();
        }

        private static readonly KunosDlcInformation FakeKunosDlc = new KunosDlcInformation(-1, "AC", "Original game", null, null);
        private static readonly KunosDlcInformation FakeModDlc = new KunosDlcInformation(-2, "MOD", "Mods", null, null);

        protected override SelectDlc GetSelectedItem(IList<SelectDlc> list, CarObject obj) {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (obj == null) return null;

            var value = obj.Dlc ?? (obj.Author == AcCommonObject.AuthorKunos ? FakeKunosDlc : FakeModDlc);
            if (value != null) {
                for (var i = list.Count - 1; i >= 0; i--) {
                    var x = list[i];
                    if (x.Information.Id == value.Id) return x;
                }
            }

            return null;
        }

        protected override SelectDlc LoadFromCache(string serialized) {
            return SelectDlc.Deserialize(serialized);
        }

        protected override void AddNewIfMissing(IList<SelectDlc> list, CarObject obj) {
            var value = obj.Dlc ?? (obj.Author == AcCommonObject.AuthorKunos ? FakeKunosDlc : FakeModDlc);
            for (var i = list.Count - 1; i >= 0; i--) {
                var item = list[i];
                if (item.Information.Id == value.Id) {
                    IncreaseCounter(obj, item);
                    return;
                }
            }

            AddNewIfMissing(list, obj, new SelectDlc(value));
        }

        protected override bool OnObjectPropertyChanged(CarObject obj, PropertyChangedEventArgs e) {
            return false;
        }

        protected override Uri GetPageAddress(SelectDlc dlc) {
            if (dlc.Information.Id == FakeKunosDlc.Id) {
                return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                        $"enabled+&k+&!dlc:", dlc.Information.DisplayName);
            }

            if (dlc.Information.Id == FakeModDlc.Id) {
                return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                        $"enabled+&k-", dlc.Information.DisplayName);
            }

            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=car&Filter={0}&Title={1}",
                    $"enabled+&dlc:{Filter.Encode(dlc.Information.ShortName)}", dlc.Information.DisplayName);
        }
    }
}
