using AcManager.Tools.AcObjectsNew;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class AcObjectTester : ITester<AcObjectNew> {
        public static AcObjectTester Instance = new AcObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "id":
                    return nameof(AcObjectNew.Id);

                case "name":
                    return nameof(AcObjectNew.Name);

                case "enabled":
                    return nameof(AcObjectNew.Enabled);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(AcObjectNew obj, string key, ITestEntry value) {
            if (key == null) {
                return value.Test(obj.Id) || value.Test(obj.DisplayName);
            }

            switch (key) {
                case "id":
                    return value.Test(obj.Id);

                case "name":
                    return value.Test(obj.Name);

                case "enabled":
                    return value.Test(obj.Enabled);
            }

            return false;
        }
    }
}