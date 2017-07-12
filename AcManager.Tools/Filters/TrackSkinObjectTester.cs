using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class TrackSkinObjectTester : ITester<TrackSkinObject> {
        public static TrackSkinObjectTester Instance = new TrackSkinObjectTester();

        public static string InnerParameterFromKey(string key) {
            /*switch (key) {
                case "skinname":
                    return nameof(TrackSkinObject.Name);

                case "driver":
                case "drivername":
                    return nameof(TrackSkinObject.DriverName);

                case "team":
                case "teamname":
                    return nameof(TrackSkinObject.Team);

                case "n":
                case "number":
                    return nameof(TrackSkinObject.SkinNumber);

                case "priority":
                    return nameof(TrackSkinObject.Priority);
            }*/

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(TrackSkinObject obj, string key, ITestEntry value) {
            /*switch (key) {
                case "skinname":
                    return value.Test(obj.Name);

                case "driver":
                case "drivername":
                    return value.Test(obj.DriverName);

                case "team":
                case "teamname":
                    return value.Test(obj.Team);

                case "n":
                case "number":
                    return value.Test(obj.SkinNumber);

                case "priority":
                    return value.Test(obj.Priority ?? 0);
            }*/

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }
    }
}