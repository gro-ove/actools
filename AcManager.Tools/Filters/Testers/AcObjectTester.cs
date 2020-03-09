using System.Collections.Generic;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.TestEntries;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class AcObjectTester : ITester<AcObjectNew>, ITesterDescription {
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

                case "note":
                case "notes":
                    return nameof(AcObjectNew.Notes);

                case "fav":
                case "favorite":
                case "favourite":
                case "favorited":
                case "favourited":
                    return nameof(AcObjectNew.IsFavourite);

                case "new":
                    return nameof(AcCommonObject.IsNew);

                case "age":
                    return nameof(AcCommonObject.Age);

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
                    return value.Test(obj.Id) || value.Test(obj.Name);

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

                case "note":
                case "notes":
                    return value.Test(obj.Notes);

                case "fav":
                case "favorite":
                case "favourite":
                case "favorited":
                case "favourited":
                    return value.Test(obj.IsFavourite);

                case "new":
                    return value.Test(obj.IsNew);

                case "age":
                    value.Set(TestEntryFactories.TimeDays);
                    return value.Test(obj.Age);

                case "date":
                    value.Set(DateTimeTestEntry.Factory);
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

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("id", "ID", KeywordType.String, KeywordPriority.Obscured),
                new KeywordDescription("name", "Name", KeywordType.String, KeywordPriority.Obscured),
                new KeywordDescription("enabled", "Enabled", KeywordType.Flag, KeywordPriority.Normal),
                new KeywordDescription("rating", "User rating", KeywordType.Number, KeywordPriority.Normal, "rate", "rated"),
                new KeywordDescription("fav", "In favorites", KeywordType.Flag, KeywordPriority.Normal, "favourite", "favorite", "favorited", "favourited"),
                new KeywordDescription("new", "Added recently", KeywordType.Flag, KeywordPriority.Important),
                new KeywordDescription("age", "Time passed since file was created", "days", KeywordType.TimeSpan, KeywordPriority.Normal),
                new KeywordDescription("date", "Creation date", KeywordType.DateTime, KeywordPriority.Obscured),
                new KeywordDescription("author", "Author", KeywordType.String, KeywordPriority.Normal, "a"),
                new KeywordDescription("kunos", "Made by Kunos", KeywordType.Flag, KeywordPriority.Important, "k"),
            };
        }
    }
}