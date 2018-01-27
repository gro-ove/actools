using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Filters.TestEntries;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class ReplayObjectTester : IParentTester<ReplayObject>, ITesterDescription {
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
                    value.Set(TestEntryFactories.FileSizeMegabytes);
                    return value.Test(obj.Size);

                case "date":
                    value.Set(DateTimeTestEntry.Factory);
                    return value.Test(obj.CreationDateTime);

                case "age":
                    value.Set(TestEntryFactories.TimeDays);
                    return value.Test(obj.Age);

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
                    return obj.Track != null && filter.Test(TrackObjectBaseTester.Instance, obj.Track);

                case "weather":
                    return obj.Weather != null && filter.Test(WeatherObjectTester.Instance, obj.Weather);
            }

            return false;
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("driver", "Driver name", KeywordType.String, KeywordPriority.Important, "drivername"),
                new KeywordDescription("size", "File size", "megabytes", KeywordType.FileSize, KeywordPriority.Normal),
                new KeywordDescription("date", "Date", KeywordType.DateTime, KeywordPriority.Normal),
                new KeywordDescription("age", "Age", "days", KeywordType.TimeSpan, KeywordPriority.Normal),
                new KeywordDescription("car", "Car", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "c"),
                new KeywordDescription("skin", "Skin", KeywordType.String | KeywordType.Child, KeywordPriority.Important),
                new KeywordDescription("track", "Track", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "t"),
                new KeywordDescription("weather", "Weather", KeywordType.String | KeywordType.Child, KeywordPriority.Important),
            }.Concat(AcCommonObjectTester.Instance.GetDescriptions());
        }
    }
}