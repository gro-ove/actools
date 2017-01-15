using System;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Pages.SelectionLists {
    public partial class CarTags {
        public CarTags() : base(CarsManager.Instance) {
            InitializeComponent();
        }

        protected override Uri GetPageAddress(SelectTag category) {
            return SelectCarDialog.TagUri(category.TagValue);
        }

        protected override bool IsIgnored(CarObject obj, string tagValue) {
            return string.Equals(obj.Brand, tagValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
