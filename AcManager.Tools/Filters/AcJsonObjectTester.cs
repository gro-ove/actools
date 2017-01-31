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

                case "a":
                case "author":
                case "k":
                case "kunos":
                    return nameof(AcJsonObjectNew.Author);

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
                    return obj.Dlc != null && (value.Test(obj.Dlc.Id) || value.Test(obj.Dlc.ShortName) || value.Test(obj.Dlc.DisplayName));

                case "desc":
                case "description":
                    return value.Test(obj.Description);

                case "c":
                case "country":
                    return value.Test(obj.Country);

                case "t":
                case "tag":
                    return obj.Tags.Any(value.Test);

                case "a":
                case "author":
                    return value.Test(obj.Author);

                case "k":
                case "kunos":
                    return value.Test(obj.Author == AcCommonObject.AuthorKunos);

                case "version":
                    return value.Test(obj.Version);

                case "url":
                    return value.Test(obj.Url);
            }

            return AcCommonObjectTester.Instance.Test(obj, key, value);
        }
    }
}