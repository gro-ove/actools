using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class CarSetupObjectTester : IParentTester<CarSetupObject> {
        public static CarSetupObjectTester Instance = new CarSetupObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "track":
                    return nameof(CarSetupObject.Track);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcCommonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(CarSetupObject obj, string key, ITestEntry value) {
            switch (key) {
                case "track":
                    return value.Test(obj.Track?.Id) || value.Test(obj.Track?.Name);
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }

        public bool TestChild(CarSetupObject obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "track":
                    return obj.Track != null && filter.Test(TrackObjectTester.Instance, obj.Track);
            }

            return false;
        }
    }
}