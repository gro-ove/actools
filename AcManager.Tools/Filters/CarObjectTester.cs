using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class CarObjectTester : IParentTester<CarObject> {
        public static CarObjectTester Instance = new CarObjectTester();

        internal static string InnerParameterFromKey(string key) {
            switch (key) {
                case "b":
                case "brand":
                case "newbrand":
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
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        private List<string> _list;

        public bool Test(CarObject obj, string key, ITestEntry value) {
            switch (key) {
                case "b":
                case "brand":
                    return value.Test(obj.Brand);

                case "newbrand":
                    if (_list == null) {
                        _list = FilesStorage.Instance.GetContentDirectory(ContentCategory.BrandBadges).Select(x => x.Name).ToList();
                    }
                    return value.Test(!_list.Contains(obj.Brand));

                case "class":
                    return value.Test(obj.CarClass);

                case "parent":
                    return value.Test(obj.Parent?.DisplayName);

                case "bhp":
                case "power":
                    return value.Test(obj.SpecsBhp);

                case "torque":
                    return value.Test(obj.SpecsTorque);

                case "weight":
                case "mass":
                    return value.Test(obj.SpecsWeight);

                case "acceleration":
                    return value.Test(obj.SpecsAcceleration);
                    
                case "speed":
                case "topspeed":
                    return value.Test(obj.SpecsTopSpeed);

                case "pw":
                case "pwratio":
                    return value.Test(obj.SpecsPwRatio);

                case "skins":
                    return value.Test(obj.SkinsEnabledWrappersList?.Count ?? 0);
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