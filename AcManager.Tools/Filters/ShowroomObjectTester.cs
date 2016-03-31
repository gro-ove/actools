using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class ShowroomObjectTester : ITester<ShowroomObject> {
        public static ShowroomObjectTester Instance = new ShowroomObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "hassound":
                    return nameof(ShowroomObject.HasSound);

                case "sound":
                    return nameof(ShowroomObject.SoundEnabled);
            }

            return null;
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(ShowroomObject obj, string key, ITestEntry value) {
            switch (key) {
                case "hassound":
                    return value.Test(obj.HasSound);

                case "sound":
                    return value.Test(obj.SoundEnabled);
            }

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }
    }
}