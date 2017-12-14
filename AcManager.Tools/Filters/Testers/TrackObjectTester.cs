using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Filters.TestEntries;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class TrackObjectTester : ITester<TrackObject>, ITesterDescription {
        public static readonly TrackObjectTester Instance = new TrackObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "layouts":
                    return nameof(TrackObject.MultiLayouts);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? TrackObjectBaseTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(TrackObject obj, string key, ITestEntry value) {
            var multiLayouts = obj.MultiLayouts;

            switch (key) {
                case "layouts":
                    return value.Test(multiLayouts?.Count ?? 1);
            }

            return multiLayouts?.Count > 1
                    ? multiLayouts.Any(x => TrackObjectBaseTester.Instance.Test(x, key, value))
                    : TrackObjectBaseTester.Instance.Test(obj, key, value);
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("layouts", "Number of layouts", KeywordType.Number, KeywordPriority.Important),
            }.Concat(TrackObjectBaseTester.Instance.GetDescriptions());
        }
    }
}