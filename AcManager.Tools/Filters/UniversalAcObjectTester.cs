using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class UniversalAcObjectTester : ITester<AcObjectNew> {
        public static readonly UniversalAcObjectTester Instance = new UniversalAcObjectTester();

        public string ParameterFromKey(string key) {
            return AcObjectTester.InnerParameterFromKey(key) ??
                   AcCommonObjectTester.InnerParameterFromKey(key) ??
                   AcJsonObjectTester.InnerParameterFromKey(key) ??
                   CarObjectTester.InnerParameterFromKey(key) ??
                   CarSkinObjectTester.InnerParameterFromKey(key) ??
                   TrackObjectTester.InnerParameterFromKey(key) ??
                   TrackObjectBaseTester.InnerParameterFromKey(key) ??
                   ShowroomObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(AcObjectNew obj, string key, ITestEntry value) {
            switch (obj.GetType().Name) {
                case nameof(CarObject):
                    return CarObjectTester.Instance.Test((CarObject)obj, key, value);
                case nameof(TrackObject):
                    return TrackObjectTester.Instance.Test((TrackObject)obj, key, value);
                case nameof(TrackObjectBase):
                case nameof(TrackExtraLayoutObject):
                    return TrackObjectBaseTester.Instance.Test((TrackObjectBase)obj, key, value);
                case nameof(ShowroomObject):
                    return ShowroomObjectTester.Instance.Test((ShowroomObject)obj, key, value);
                case nameof(CarSkinObject):
                    return CarSkinObjectTester.Instance.Test((CarSkinObject)obj, key, value);
                case nameof(WeatherObject):
                    return WeatherObjectTester.Instance.Test((WeatherObject)obj, key, value);
                case nameof(CarSetupObject):
                    return CarSetupObjectTester.Instance.Test((CarSetupObject)obj, key, value);
                case nameof(ReplayObject):
                    return ReplayObjectTester.Instance.Test((ReplayObject)obj, key, value);
                default:
                    return AcObjectTester.Instance.Test(obj, key, value);
            }
        }
    }
}