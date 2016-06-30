using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class ReplayObjectTester : IParentTester<ReplayObject> {
        public static ReplayObjectTester Instance = new ReplayObjectTester();

        public string ParameterFromKey(string key) {
            return AcCommonObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(ReplayObject obj, string key, ITestEntry value) {
            switch (key) {
                case "driver":
                case "drivername":
                    return value.Test(obj.DriverName);

                case "size":
                    return value.Test(obj.Size.AsMegabytes());

                case "carid":
                    return value.Test(obj.CarId);

                case "car":
                    return value.Test(obj.CarId) || value.Test(obj.Car?.DisplayName);

                case "skinid":
                    return value.Test(obj.CarSkinId);

                case "skin":
                    return value.Test(obj.CarSkinId) || value.Test(obj.CarSkin?.DisplayName);

                case "trackid":
                    return value.Test(obj.TrackId);

                case "track":
                    return value.Test(obj.TrackId) || value.Test(obj.Track?.Name);

                case "weatherid":
                    return value.Test(obj.WeatherId);

                case "weather":
                    return value.Test(obj.WeatherId) || value.Test(obj.Weather?.Name);
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }

        public bool TestChild(ReplayObject obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "car":
                    return obj.Car != null && filter.Test(CarObjectTester.Instance, obj.Car);

                case "skin":
                    return obj.CarSkin != null && filter.Test(CarSkinObjectTester.Instance, obj.CarSkin);

                case "track":
                    return obj.Track != null && filter.Test(TrackBaseObjectTester.Instance, obj.Track);

                case "weather":
                    return obj.Weather != null && filter.Test(WeatherObjectTester.Instance, obj.Weather);
            }

            return false;
        }
    }
}