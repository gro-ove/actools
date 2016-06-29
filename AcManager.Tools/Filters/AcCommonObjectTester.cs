using AcManager.Tools.AcObjectsNew;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class AcCommonObjectTester : ITester<AcCommonObject> {
        public static AcCommonObjectTester Instance = new AcCommonObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "y":
                case "year":
                    return nameof(AcCommonObject.Year);
                    
                case "new":
                    return nameof(AcCommonObject.IsNew);

                case "errors":
                case "haserrors":
                    return nameof(AcCommonObject.HasErrors);

                case "changed":
                    return nameof(AcCommonObject.Changed);
            }

            return null;
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(AcCommonObject obj, string key, ITestEntry value) {
            switch (key) {
                case "y":
                case "year":
                    return value.Test(obj.Year ?? 0);

                case "new":
                    return value.Test(obj.IsNew);

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