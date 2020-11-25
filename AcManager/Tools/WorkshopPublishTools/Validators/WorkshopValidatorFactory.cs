using System;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;

namespace AcManager.Tools.WorkshopPublishTools.Validators {
    public static class WorkshopValidatorFactory {
        public static IWorkshopValidator Create<T>(T target, bool isChildObject) where T : AcJsonObjectNew {
            switch ((AcJsonObjectNew)target) {
                case CarObject car: return new WorkshopCarValidator(car, isChildObject);
                case CarSkinObject carSkin: return new WorkshopCarSkinValidator(carSkin, isChildObject);
                default: throw new NotImplementedException("Not supported type: " + typeof(T).Name);
            }
        }
    }
}