using System;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public static class WorkshopSubmitterFactory {
        public static IWorkshopSubmitter Create<T>(T target, WorkshopSubmitterParams submitterParams) where T : AcJsonObjectNew {
            switch ((AcJsonObjectNew)target) {
                case CarObject car:
                    return new WorkshopCarSubmitter(car, false, submitterParams);
                default:
                    throw new NotImplementedException("Not supported type: " + typeof(T).Name);
            }
        }
    }
}