using System.Linq;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class CarObjectTester : IParentTester<CarObject> {
        public static CarObjectTester Instance = new CarObjectTester();

        internal static string InnerParameterFromKey(string key) {
            switch (key) {
                case "b":
                case "brand":
                    return nameof(CarObject.Brand);

                case "class":
                    return nameof(CarObject.CarClass);

                case "parent":
                    return nameof(CarObject.Parent);

                case "bhp":
                case "power":
                    return nameof(CarObject.SpecsBhp);

                case "torque":
                    return nameof(CarObject.SpecsTorque);

                case "weight":
                case "mass":
                    return nameof(CarObject.SpecsWeight);

                case "acceleration":
                    return nameof(CarObject.SpecsAcceleration);

                case "speed":
                case "topspeed":
                    return nameof(CarObject.SpecsTopSpeed);

                case "pw":
                case "pwratio":
                    return nameof(CarObject.SpecsPwRatio);
                    
                case "skin":
                case "skins":
                    return nameof(CarObject.SkinsEnabledWrappersList);

                default:
                    return null;
            }
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(CarObject obj, string key, ITestEntry value) {
            switch (key) {
                case "b":
                case "brand":
                    return obj.Brand != null && value.Test(obj.Brand);

                case "class":
                    return obj.CarClass != null && value.Test(obj.CarClass);

                case "parent":
                    return obj.Parent != null && value.Test(obj.Parent.DisplayName);

                case "bhp":
                case "power":
                    return obj.SpecsBhp != null && value.Test(obj.SpecsBhp);

                case "torque":
                    return obj.SpecsTorque != null && value.Test(obj.SpecsTorque);

                case "weight":
                case "mass":
                    return obj.SpecsWeight != null && value.Test(obj.SpecsWeight);

                case "acceleration":
                    return obj.SpecsAcceleration != null && value.Test(obj.SpecsAcceleration);

                case "speed":
                case "topspeed":
                    return obj.SpecsTopSpeed != null && value.Test(obj.SpecsTopSpeed);

                case "pw":
                case "pwratio":
                    return obj.SpecsPwRatio != null && value.Test(obj.SpecsPwRatio);

                case "skins":
                    return obj.SkinsEnabledWrappersList?.Count > 0 && value.Test(obj.SkinsEnabledWrappersList.Count);
            }

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }

        public bool TestChild(CarObject obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "skin":
                    return obj.Skins?.Any(x => filter.Test(CarSkinObjectTester.Instance, x)) == true;

                case "parent":
                    return obj.Parent != null && filter.Test(Instance, obj.Parent);
            }

            return false;
        }
    }
}