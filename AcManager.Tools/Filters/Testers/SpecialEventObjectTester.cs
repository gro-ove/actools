using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class SpecialEventObjectTester : IParentTester<SpecialEventObject>, ITesterDescription {
        public static readonly SpecialEventObjectTester Instance = new SpecialEventObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "type":
                    return nameof(SpecialEventObject.DisplayType);

                case "guid":
                    return nameof(SpecialEventObject.Guid);

                case "c":
                case "car":
                case "carid":
                    return nameof(SpecialEventObject.CarId);

                case "skin":
                case "skinid":
                    return nameof(SpecialEventObject.CarSkinId);

                case "t":
                case "track":
                case "trackid":
                    return nameof(SpecialEventObject.TrackId);

                case "weather":
                case "weatherid":
                    return nameof(SpecialEventObject.WeatherId);

                case "passed":
                case "won":
                case "place":
                    return nameof(SpecialEventObject.TakenPlace);

                case "fpt":
                case "firstplacetarget":
                    return nameof(SpecialEventObject.FirstPlaceTarget);

                case "spt":
                case "secondplacetarget":
                    return nameof(SpecialEventObject.SecondPlaceTarget);

                case "tpt":
                case "thirdplacetarget":
                    return nameof(SpecialEventObject.ThirdPlaceTarget);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcCommonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(SpecialEventObject obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(obj.Id) || value.Test(obj.DisplayName) || value.Test(obj.DisplayType);

                case "type":
                    return value.Test(obj.DisplayType);

                case "guid":
                    return value.Test(obj.Guid);

                case "carid":
                    return value.Test(obj.CarId);

                case "c":
                case "car":
                    return value.Test(obj.CarObject?.Id) || value.Test(obj.CarObject?.Name);

                case "skinid":
                    return value.Test(obj.CarSkinId);

                case "skin":
                    return value.Test(obj.CarSkin?.Id) || value.Test(obj.CarSkin?.Name);

                case "trackid":
                    return value.Test(obj.TrackId);

                case "t":
                case "track":
                    return value.Test(obj.TrackObject?.Id) || value.Test(obj.TrackObject?.Name);

                case "weatherid":
                    return value.Test(obj.WeatherId);

                case "weather":
                    return value.Test(obj.WeatherObject?.Id) || value.Test(obj.WeatherObject?.Name);

                case "passed":
                    return value.Test(obj.TakenPlace != 5);

                case "won":
                    return value.Test(obj.TakenPlace == 1);

                case "place":
                    return value.Test(obj.TakenPlace);

                case "fpt":
                case "firstplacetarget":
                    return value.Test(obj.FirstPlaceTarget ?? 0);

                case "spt":
                case "secondplacetarget":
                    return value.Test(obj.SecondPlaceTarget ?? 0);

                case "tpt":
                case "thirdplacetarget":
                    return value.Test(obj.ThirdPlaceTarget ?? 0);
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }

        public bool TestChild(SpecialEventObject obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "car":
                    return obj.CarObject != null && filter.Test(CarObjectTester.Instance, obj.CarObject);

                case "skin":
                    return obj.CarSkin != null && filter.Test(CarSkinObjectTester.Instance, obj.CarSkin);

                case "track":
                    return obj.TrackObject != null && filter.Test(TrackObjectBaseTester.Instance, obj.TrackObject);
            }

            return false;
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("type", "Type", KeywordType.String, KeywordPriority.Important),
                new KeywordDescription("guid", "GUID", KeywordType.String, KeywordPriority.Obscured),

                new KeywordDescription("car", "Car", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "c"),
                new KeywordDescription("skin", "Skin", KeywordType.String | KeywordType.Child, KeywordPriority.Important),
                new KeywordDescription("track", "Track", KeywordType.String | KeywordType.Child, KeywordPriority.Important, "t"),
                new KeywordDescription("weather", "Weather", KeywordType.String | KeywordType.Child, KeywordPriority.Important),

                new KeywordDescription("passed", "Passed", KeywordType.Flag, KeywordPriority.Important),
                new KeywordDescription("won", "Won", KeywordType.Flag, KeywordPriority.Important),
                new KeywordDescription("place", "Taken place", KeywordType.Number, KeywordPriority.Important),

                new KeywordDescription("firstplacetarget", "Target for first place", KeywordType.Number, KeywordPriority.Obscured, "fpt"),
                new KeywordDescription("secondplacetarget", "Target for second place", KeywordType.Number, KeywordPriority.Obscured, "spt"),
                new KeywordDescription("thirdplacetarget", "Target for third place", KeywordType.Number, KeywordPriority.Obscured, "tpt")
            }.Concat(AcCommonObjectTester.Instance.GetDescriptions());
        }
    }
}