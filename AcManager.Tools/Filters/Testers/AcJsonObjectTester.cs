using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcObjectsNew;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class AcJsonObjectTester : ITester<AcJsonObjectNew>, ITesterDescription {
        public static readonly AcJsonObjectTester Instance = new AcJsonObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "desc":
                case "description":
                    return nameof(AcJsonObjectNew.Description);

                case "c":
                case "country":
                    return nameof(AcJsonObjectNew.Country);

                case "t":
                case "tag":
                    return nameof(AcJsonObjectNew.Tags);

                case "version":
                    return nameof(AcJsonObjectNew.Version);

                case "url":
                    return nameof(AcJsonObjectNew.Url);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcCommonObjectTester.InheritingParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(AcJsonObjectNew obj, string key, ITestEntry value) {
            switch (key) {
                case "dlc":
                    var dlc = obj.Dlc;
                    return value.Test(dlc?.Id ?? 0) || value.Test(dlc?.ShortName) || value.Test(dlc?.DisplayName);

                case "desc":
                case "description":
                    return value.Test(obj.Description);

                case "c":
                case "country":
                    return value.Test(obj.Country);

                case "t":
                case "tag":
                    return obj.Tags.Any(value.Test);

                case "version":
                    return value.Test(obj.Version);

                case "url":
                    return value.Test(obj.Url);
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("dlc", "DLC name or ID", KeywordType.String, KeywordPriority.Important),
                new KeywordDescription("desc", "Description", KeywordType.String, KeywordPriority.Normal, "description"),
                new KeywordDescription("country", "Country", KeywordType.String, KeywordPriority.Normal, "c"),
                new KeywordDescription("tag", "Tag", KeywordType.String, KeywordPriority.Important, "t", "#"),
                new KeywordDescription("version", "Version", KeywordType.String, KeywordPriority.Normal),
                new KeywordDescription("url", "URL", KeywordType.String, KeywordPriority.Normal),
            }.Concat(AcCommonObjectTester.Instance.GetDescriptions());
        }
    }
}