using System.Linq;
using AcManager.Tools.AcObjectsNew;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class AcJsonObjectTester : ITester<AcJsonObjectNew> {
        public static AcJsonObjectTester Instance = new AcJsonObjectTester();

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

                case "author":
                    return nameof(AcJsonObjectNew.Author);

                case "version":
                    return nameof(AcJsonObjectNew.Version);

                case "url":
                    return nameof(AcJsonObjectNew.Url);
            }

            return null;
        }

        public string ParameterFromKey(string key) {
            return InnerParameterFromKey(key) ?? AcCommonObjectTester.InnerParameterFromKey(key);
        }

        public bool Test(AcJsonObjectNew obj, string key, ITestEntry value) {
            switch (key) {
                case "desc":
                case "description":
                    return obj.Description != null && value.Test(obj.Description);

                case "c":
                case "country":
                    return obj.Country != null && value.Test(obj.Country);

                case "t":
                case "tag":
                    return obj.Tags.Any(value.Test);

                case "author":
                    return obj.Author != null && value.Test(obj.Author);

                case "version":
                    return obj.Version != null && value.Test(obj.Version);

                case "url":
                    return obj.Url != null && value.Test(obj.Url);
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }
    }
}