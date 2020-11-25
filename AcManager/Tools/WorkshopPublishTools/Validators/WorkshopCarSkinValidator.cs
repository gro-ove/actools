using System.Collections.Generic;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Tools.WorkshopPublishTools.Validators {
    public class WorkshopCarSkinValidator : WorkshopBaseValidator<CarSkinObject> {
        public WorkshopCarSkinValidator(CarSkinObject obj, bool isChildObject) : base(obj, isChildObject) {
            FlexibleId = true;
        }

        private WorkshopValidatedItem TestSkinNumber() {
            var originalSkinNumber = Target.SkinNumber;
            return ValidateNumber("Skin number", Target.SkinNumber.As<int?>(), 0, 9999,
                    0, value => Target.SkinNumber = value?.ToInvariantString() ?? originalSkinNumber);
        }

        protected override WorkshopValidatedItem TestName() {
            var originalName = Target.NameEditable;
            var name = originalName?.Trim();
            if (string.IsNullOrEmpty(name)) {
                var newName = Target.NameFromId;
                return new WorkshopValidatedItem($"Name will be “{newName}”",
                        () => Target.NameEditable = newName, () => Target.NameEditable = originalName);
            }
            return base.TestName();
        }

        public override IEnumerable<WorkshopValidatedItem> Validate() {
            foreach (var item in base.Validate()) {
                yield return item;
            }

            yield return TestSkinNumber();
            yield return ValidateStringSimple("Driver name", Target.DriverName, 1, 120, false);
            yield return ValidateStringSimple("Team name", Target.DriverName, 1, 120, false);
            yield return ValidateFileExistance("livery.png", 100e3);
        }
    }
}