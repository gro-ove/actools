using AcManager.Tools.Objects;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class CarSkinObjectTester : ITester<CarSkinObject> {
        public static CarSkinObjectTester Instance = new CarSkinObjectTester();
        
        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "skinname":
                    return nameof(CarSkinObject.Name);

                    // TODO
            }

            return null;
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(CarSkinObject obj, string key, ITestEntry value) {
            switch (key) {
                case "skinname":
                    return obj.Name != null && value.Test(obj.Name);

                    // TODO
            }

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }
    }
}