using AcManager.Tools.AcObjectsNew;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class AcCommonObjectTester : ITester<AcCommonObject> {
        public static AcCommonObjectTester Instance = new AcCommonObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "year":
                    return nameof(AcCommonObject.Year);
            }

            return null;
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(AcCommonObject obj, string key, ITestEntry value) {
            switch (key) {
                case "year":
                    return obj.Year.HasValue && value.Test(obj.Year.Value);
            }

            return AcObjectTester.Instance.Test(obj, key, value);
        }
    }
}