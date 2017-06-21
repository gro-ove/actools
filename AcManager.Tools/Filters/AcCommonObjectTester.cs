using AcManager.Tools.AcObjectsNew;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class AcCommonObjectTester : ITester<AcCommonObject> {
        public static readonly AcCommonObjectTester Instance = new AcCommonObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "y":
                case "year":
                    return nameof(AcCommonObject.Year);

                case "errors":
                case "haserrors":
                    return nameof(AcCommonObject.HasErrors);

                case "changed":
                    return nameof(AcCommonObject.Changed);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(AcCommonObject obj, string key, ITestEntry value) {
            switch (key) {
                case "y":
                case "year":
                    return obj.Year.HasValue && value.Test(obj.Year.Value);

                case "errors":
                case "haserrors":
                    return value.Test(obj.HasErrors);

                case "changed":
                    return value.Test(obj.Changed);
            }

            return AcObjectTester.Instance.Test(obj, key, value);
        }
    }
}