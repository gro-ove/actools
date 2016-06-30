using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class TrackObjectTester : ITester<TrackObject> {
        public static TrackObjectTester Instance = new TrackObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "layouts":
                    return nameof(TrackObject.MultiLayouts);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? TrackBaseObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(TrackObject obj, string key, ITestEntry value) {
            switch (key) {
                case "layouts":
                    return value.Test(obj.MultiLayouts?.Count ?? 1);
            }

            return TrackBaseObjectTester.Instance.Test(obj, key, value);
        }
    }
}