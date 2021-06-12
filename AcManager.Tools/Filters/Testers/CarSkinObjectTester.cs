using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class CarSkinObjectTester : ITester<CarSkinObject>, ITesterDescription {
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

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(CarSkinObject obj, string key, ITestEntry value) {
            switch (key) {
                case "skinname":
                    return value.Test(obj.Name);

                case "driver":
                case "drivername":
                    return value.Test(obj.DriverName);

                case "team":
                case "teamname":
                    return value.Test(obj.Team);

                case "n":
                case "number":
                    return value.Test(obj.SkinNumber);

                case "priority":
                    return value.Test(obj.Priority ?? 0);
            }

            obj.FullDetailsRequired();
            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("name", "Name", KeywordType.String, KeywordPriority.Obscured, "skinname"),
                new KeywordDescription("driver", "Driver name", KeywordType.String, KeywordPriority.Normal, "drivername"),
                new KeywordDescription("team", "Team name", KeywordType.String, KeywordPriority.Normal, "teamname"),
                new KeywordDescription("number", "Number", KeywordType.Number, KeywordPriority.Normal, "n"),
                new KeywordDescription("priority", "Priority", KeywordType.Number, KeywordPriority.Obscured),
            }.Concat(AcJsonObjectTester.Instance.GetDescriptions());
        }
    }
}