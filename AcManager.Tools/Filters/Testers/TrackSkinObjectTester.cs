using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class TrackSkinObjectTester : ITester<TrackSkinObject>, ITesterDescription {
        public static TrackSkinObjectTester Instance = new TrackSkinObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "category":
                    return nameof(TrackSkinObject.Categories);

                case "active":
                    return nameof(TrackSkinObject.IsActive);

                case "priority":
                    return nameof(TrackSkinObject.Priority);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcJsonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(TrackSkinObject obj, string key, ITestEntry value) {
            switch (key) {
                case "category":
                    return obj.Categories.Any(value.Test);

                case "active":
                    return value.Test(obj.IsActive);

                case "priority":
                    return value.Test(obj.Priority);
            }

            return AcJsonObjectTester.Instance.Test(obj, key, value);
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("category", "Category", KeywordType.String, KeywordPriority.Important),
                new KeywordDescription("active", "Active", KeywordType.Flag, KeywordPriority.Normal),
                new KeywordDescription("priority", "Priority", KeywordType.Number, KeywordPriority.Normal),
            }.Concat(AcJsonObjectTester.Instance.GetDescriptions());
        }
    }
}