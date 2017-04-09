using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class SpecialEventObjectTester : IParentTester<SpecialEventObject> {
        public static readonly SpecialEventObjectTester Instance = new SpecialEventObjectTester();

        public string ParameterFromKey(string key) {
            switch (key) {
                case "y":
                case "year":
                    return nameof(AcCommonObject.Year);

                case "new":
                    return nameof(AcCommonObject.IsNew);

                case "age":
                    return nameof(AcCommonObject.AgeInDays);

                case "date":
                    return nameof(AcCommonObject.CreationDateTime);

                case "errors":
                case "haserrors":
                    return nameof(AcCommonObject.HasErrors);

                case "changed":
                    return nameof(AcCommonObject.Changed);

                case "type":
                    return nameof(SpecialEventObject.DisplayType);

                case "car":
                case "carid":
                    return nameof(SpecialEventObject.CarId);

                case "track":
                case "trackid":
                    return nameof(SpecialEventObject.TrackId);

                case "passed":
                case "won":
                case "place":
                    return nameof(SpecialEventObject.TakenPlace);

                case "firstplacetarget":
                    return nameof(SpecialEventObject.FirstPlaceTarget);

                case "secondplacetarget":
                    return nameof(SpecialEventObject.SecondPlaceTarget);

                case "thirdplacetarget":
                    return nameof(SpecialEventObject.ThirdPlaceTarget);
            }

            return null;
        }

        public bool Test(SpecialEventObject obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(obj.Id) || value.Test(obj.DisplayName) || value.Test(obj.DisplayType);

                case "type":
                    return value.Test(obj.DisplayType);

                case "carid":
                    return value.Test(obj.CarId);

                case "car":
                    return value.Test(obj.CarObject?.Id) || value.Test(obj.CarObject?.Name);

                case "trackid":
                    return value.Test(obj.TrackId);

                case "track":
                    return value.Test(obj.TrackObject?.Id) || value.Test(obj.TrackObject?.Name);

                case "y":
                case "year":
                    return obj.Year.HasValue && value.Test(obj.Year.Value);

                case "new":
                    return value.Test(obj.IsNew);

                case "age":
                    return value.Test(obj.AgeInDays);

                case "date":
                    return value.Test(obj.CreationDateTime);

                case "errors":
                case "haserrors":
                    return value.Test(obj.HasErrors);

                case "changed":
                    return value.Test(obj.Changed);

                case "passed":
                    return value.Test(obj.TakenPlace != 5);

                case "won":
                    return value.Test(obj.TakenPlace == 1);

                case "place":
                    return value.Test(obj.TakenPlace);

                case "firstplacetarget":
                    return value.Test(obj.FirstPlaceTarget ?? 0);

                case "secondplacetarget":
                    return value.Test(obj.SecondPlaceTarget ?? 0);

                case "thirdplacetarget":
                    return value.Test(obj.ThirdPlaceTarget ?? 0);
            }

            return AcObjectTester.Instance.Test(obj, key, value);
        }

        public bool TestChild(SpecialEventObject obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "car":
                    return obj.CarObject != null && filter.Test(CarObjectTester.Instance, obj.CarObject);

                case "track":
                    return obj.TrackObject != null && filter.Test(TrackBaseObjectTester.Instance, obj.TrackObject);
            }

            return false;
        }
    }
}