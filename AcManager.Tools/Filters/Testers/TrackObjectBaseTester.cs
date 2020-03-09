using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Filters.TestEntries;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class TrackObjectBaseTester : ITester<TrackObjectBase>, ITesterDescription {
        public static readonly TrackObjectBaseTester Instance = new TrackObjectBaseTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "city":
                    return nameof(TrackObjectBase.City);

                case "geotags":
                    return nameof(TrackObjectBase.GeoTags);

                case "len":
                case "length":
                    return nameof(TrackObjectBase.SpecsLength);

                case "width":
                    return nameof(TrackObjectBase.SpecsWidth);

                case "pits":
                case "pitboxes":
                    return nameof(TrackObjectBase.SpecsPitboxes);

                case "dd":
                case "driven":
                case "drivendistance":
                    return nameof(TrackObjectBase.TotalDrivenDistance);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(TrackObjectBase obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(obj.Id) || value.Test(obj.LayoutName);

                case "city":
                    return value.Test(obj.City);

                case "geotags":
                    return value.Test(obj.GeoTags?.ToString());

                case "len":
                case "length":
                    value.Set(TestEntryFactories.DistanceKilometers);
                    return  value.Test(obj.SpecsLengthValue / 1e3);

                case "width":
                    value.Set(TestEntryFactories.DistanceMeters);
                    return value.Test(obj.SpecsWidth);

                case "pits":
                case "pitboxes":
                    return value.Test(obj.SpecsPitboxesValue);

                case "dd":
                case "driven":
                case "drivendistance":
                    value.Set(TestEntryFactories.DistanceKilometers);
                    return value.Test(obj.TotalDrivenDistance / 1e3);
            }

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("city", "City", KeywordType.String, KeywordPriority.Normal),
                new KeywordDescription("geotags", "Geo tags", KeywordType.String, KeywordPriority.Normal),
                new KeywordDescription("len", "Length", "kilometres", KeywordType.Distance, KeywordPriority.Important, "length"),
                new KeywordDescription("width", "Width", "metres", KeywordType.Distance, KeywordPriority.Normal),
                new KeywordDescription("pits", "Pit boxes", KeywordType.Number, KeywordPriority.Important, "pitboxes"),
            }.Concat(AcJsonObjectTester.Instance.GetDescriptions());
        }
    }
}