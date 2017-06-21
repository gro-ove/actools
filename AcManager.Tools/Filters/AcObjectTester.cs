using AcManager.Tools.AcObjectsNew;
using StringBasedFilter;

namespace AcManager.Tools.Filters {
    public class AcObjectTester : ITester<AcObjectNew> {
        public static readonly AcObjectTester Instance = new AcObjectTester();

        public static string InnerParameterFromKey(string key) {
            switch (key) {
                case "id":
                    return nameof(AcObjectNew.Id);

                case "name":
                    return nameof(AcObjectNew.Name);

                case "enabled":
                    return nameof(AcObjectNew.Enabled);

                case "rate":
                case "rated":
                case "rating":
                    return nameof(AcObjectNew.Rating);

                case "fav":
                case "favorite":
                case "favourite":
                case "favorited":
                case "favourited":
                    return nameof(AcObjectNew.IsFavourite);

                case "new":
                    return nameof(AcCommonObject.IsNew);

                case "age":
                    return nameof(AcCommonObject.AgeInDays);

                case "date":
                    return nameof(AcCommonObject.CreationDateTime);

                case "a":
                case "author":
                case "k":
                case "kunos":
                    return nameof(IAcObjectAuthorInformation.Author);

                case null:
                    return nameof(AcObjectNew.DisplayName);
            }

            return null;
        }

        public static string InheritingParameterFromKey(string key) {
            return InnerParameterFromKey(key);
        }

        public string ParameterFromKey(string key) {
            return InheritingParameterFromKey(key);
        }

        public bool Test(AcObjectNew obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(obj.Id) || value.Test(obj.DisplayName);

                case "id":
                    return value.Test(obj.Id);

                case "name":
                    return value.Test(obj.Name);

                case "enabled":
                    return value.Test(obj.Enabled);

                case "rate":
                case "rated":
                case "rating":
                    return value.Test(obj.Rating ?? 0d);

                case "fav":
                case "favorite":
                case "favourite":
                case "favorited":
                case "favourited":
                    return value.Test(obj.IsFavourite);

                case "new":
                    return value.Test(obj.IsNew);

                case "age":
                    return value.Test(obj.AgeInDays);

                case "date":
                    return value.Test(obj.CreationDateTime);

                case "a":
                case "author":
                    return value.Test((obj as IAcObjectAuthorInformation)?.Author);

                case "k":
                case "kunos":
                    return value.Test((obj as IAcObjectAuthorInformation)?.Author == AcCommonObject.AuthorKunos);

                default:
                    return false;
            }
        }
    }
}