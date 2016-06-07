using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class CarSkinObjectTester : ITester<CarSkinObject> {
        public static CarSkinObjectTester Instance = new CarSkinObjectTester();
        
        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "skinname":
                    return nameof(CarSkinObject.Name);

                case "driver":
                case "drivername":
                    return nameof(CarSkinObject.DriverName);

                case "team":
                case "teamname":
                    return nameof(CarSkinObject.Team);

                case "n":
                case "number":
                    return nameof(CarSkinObject.SkinNumber);

                case "priority":
                    return nameof(CarSkinObject.Priority);
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

                case "driver":
                case "drivername":
                    return obj.DriverName != null && value.Test(obj.DriverName);

                case "team":
                case "teamname":
                    return obj.Team != null && value.Test(obj.Team);

                case "n":
                case "number":
                    return obj.SkinNumber != null && value.Test(obj.SkinNumber);

                case "priority":
                    return obj.Priority.HasValue && value.Test(obj.Priority.Value);
            }

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }
    }
}