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

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? TrackBaseObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(TrackObject obj, string key, ITestEntry value) {
            switch (key) {
                case "layouts":
                    return obj.MultiLayouts != null && value.Test(obj.MultiLayouts.Count);
            }

            return TrackBaseObjectTester.Instance.Test(obj, key, value);
        }
    }
}