using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.WorkshopPublishTools.Validators {
    public interface IWorkshopValidator {
        [NotNull, ItemNotNull]
        IEnumerable<WorkshopValidatedItem> Validate();
    }

    public class WorkshopBaseValidator<T> : IWorkshopValidator where T : AcJsonObjectNew {
        protected T Target { get; }

        protected bool IsChildObject { get; }

        protected int MaxTagsCount { get; set; } = 20;

        protected bool YearRequired { get; set; }

        protected bool CountryRequired { get; set; }

        protected bool DescriptionRequired { get; set; }

        protected bool FlexibleId { get; set; }

        protected bool NameCanContainYearPostfix { get; set; }

        protected WorkshopBaseValidator(T obj, bool isChildObject) {
            Target = obj;
            IsChildObject = isChildObject;
        }

        protected WorkshopValidatedItem ValidateNumber(string valueName, int? value, int minValue, int maxValue, int fallbackValue, Action<int?> fallbackUpdate) {
            if (value.HasValue) {
                return value < minValue || value > maxValue
                        ? new WorkshopValidatedItem($"Incorrect {valueName.ToSentenceMember()}", WorkshopValidatedState.Failed)
                        : new WorkshopValidatedItem($"{valueName} is correct");
            }
            return new WorkshopValidatedItem($"{valueName} will be set to {fallbackValue}",
                    () => fallbackUpdate(fallbackValue), () => fallbackUpdate(null));
        }

        protected WorkshopValidatedItem ValidateStringSimple(string valueName, [CanBeNull] string value, int minLength, int maxLength, bool isRequired) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return value.Length < minLength
                        ? new WorkshopValidatedItem($"Value of {valueName.ToSentenceMember()} is too short", WorkshopValidatedState.Failed)
                        : value.Length > maxLength
                                ? new WorkshopValidatedItem($"Value of {valueName.ToSentenceMember()} is too long", WorkshopValidatedState.Failed)
                                : new WorkshopValidatedItem($"{valueName} is correct");
            }
            return isRequired
                    ? new WorkshopValidatedItem($"{valueName} is required", WorkshopValidatedState.Failed)
                    : new WorkshopValidatedItem($"{valueName} is not set, but it’s optional");
        }

        protected WorkshopValidatedItem ValidateFileExistance(string fileName, double maxLength) {
            var fileInfo = new FileInfo(Path.Combine(Target.Location, fileName));
            return !fileInfo.Exists
                    ? new WorkshopValidatedItem($"File “{fileName}” is required", WorkshopValidatedState.Failed)
                    : fileInfo.Length > maxLength
                            ? new WorkshopValidatedItem($"File “{fileName}” is too large", WorkshopValidatedState.Failed)
                            : new WorkshopValidatedItem($"File “{fileName}” exists");
        }

        protected virtual WorkshopValidatedItem TestId() {
            if (!Regex.IsMatch(Target.Id, FlexibleId ? @"^[\w. -]+$" : @"^[_a-z\d\.-]+$")) {
                return new WorkshopValidatedItem(FlexibleId
                        ? "ID can only contain letters, digits, “_”, “-” and “.”"
                        : "ID can only contain lower case letters, digits, “_”, “-” and “.”", WorkshopValidatedState.Failed);
            }
            if (Target.Id.Length > 100) {
                return new WorkshopValidatedItem("ID is too long", WorkshopValidatedState.Failed);
            }
            return new WorkshopValidatedItem("ID is correct");
        }

        protected virtual WorkshopValidatedItem TestName() {
            var originalName = Target.NameEditable;
            var name = originalName?.Trim();
            if (string.IsNullOrEmpty(name)) {
                return new WorkshopValidatedItem("Name is required", WorkshopValidatedState.Failed);
            }
            if (name.Length > 100) {
                return new WorkshopValidatedItem("Name is too long", WorkshopValidatedState.Failed);
            }
            if (name != Target.Name || Regex.IsMatch(name, @"[\t\n\r]")
                    || !NameCanContainYearPostfix && Regex.IsMatch(name, $@"\s(['`’]\d\d|{Target.Year})$")) {
                return new WorkshopValidatedItem("Name needs cleaning up", () => {
                    Target.NameEditable = Regex.Replace(originalName.Trim(), @"\s+", " ");
                    if (!NameCanContainYearPostfix) {
                        Target.NameEditable = Regex.Replace(Target.NameEditable, $@"\s(['`’]\d\d|{Target.Year})$", "").TrimEnd();
                    }
                }, () => Target.NameEditable = originalName);
            }
            return new WorkshopValidatedItem("Name is correct");
        }

        protected virtual WorkshopValidatedItem TestVersion() {
            if (Target.Version == null) {
                return new WorkshopValidatedItem("Version will be set to 0",
                        () => Target.Version = "0", () => Target.Version = null);
            }

            if (!Regex.IsMatch(Target.Version, @"^\d")) {
                return new WorkshopValidatedItem("Start version with a digit", WorkshopValidatedState.Warning);
            }

            return new WorkshopValidatedItem("Version is correct");
        }

        protected virtual WorkshopValidatedItem TestUrl() {
            var originalUrl = Target.Url;
            if (string.IsNullOrWhiteSpace(originalUrl)) {
                return !string.IsNullOrEmpty(originalUrl)
                        ? new WorkshopValidatedItem("URL shouldn’t be of spaces only",
                                () => Target.Url = string.Empty, () => Target.Url = originalUrl)
                        : new WorkshopValidatedItem("URL is not set, but it’s optional");
            }

            if (originalUrl.IsWebUrl()) {
                return new WorkshopValidatedItem("URL is correct");
            }

            if (originalUrl.StartsWith(@"www.")) {
                var newUrl = $"https://{originalUrl}";
                return new WorkshopValidatedItem($"URL will be changed to “{newUrl}”",
                        () => Target.Url = newUrl, () => Target.Url = originalUrl);
            }

            return new WorkshopValidatedItem("Incorrect URL value", WorkshopValidatedState.Failed);
        }

        [CanBeNull]
        protected virtual string GuessCountry() {
            return Target.Tags.Select(AcStringValues.CountryFromTag).FirstOrDefault(x => x != null);
        }

        protected virtual WorkshopValidatedItem TestCountry() {
            var originalCountry = Target.Country;
            var country = originalCountry != null ? AcStringValues.CountryFromTag(originalCountry) : GuessCountry();
            if (country != null) {
                return country != originalCountry
                        ? new WorkshopValidatedItem($"There might be a typo in the country’s name, will be changed to “{country}”",
                                () => Target.Country = country, () => Target.Country = originalCountry)
                        : new WorkshopValidatedItem("Country is correct");
            }

            if (originalCountry != null) {
                return new WorkshopValidatedItem("There might be a typo in the country’s name, it’s unknown to CM", WorkshopValidatedState.Failed);
            }

            return CountryRequired
                    ? new WorkshopValidatedItem("Country is required", WorkshopValidatedState.Failed)
                    : new WorkshopValidatedItem("Country is not set, but it’s optional");
        }

        protected virtual WorkshopValidatedItem TestDescription() {
            var originalDescription = Target.Description;
            if (string.IsNullOrWhiteSpace(originalDescription)) {
                if (DescriptionRequired) {
                    return new WorkshopValidatedItem("Description is required", WorkshopValidatedState.Failed);
                }

                return !string.IsNullOrEmpty(originalDescription)
                        ? new WorkshopValidatedItem("Description shouldn’t be of spaces only",
                                () => Target.Description = string.Empty, () => Target.Description = originalDescription)
                        : new WorkshopValidatedItem("Description is not set, but it’s optional");
            }

            return originalDescription != originalDescription.ToSentence()
                    ? originalDescription.Length < 100
                            ? new WorkshopValidatedItem("Description should be at least a single complete sentence", WorkshopValidatedState.Failed)
                            : new WorkshopValidatedItem("Description will be slightly altered to be a proper text",
                                    () => Target.Description = originalDescription.ToSentence(),
                                    () => Target.Description = originalDescription)
                    : new WorkshopValidatedItem("Description is correct");
        }

        protected virtual Tuple<int, int> GetYearBoundaries() {
            return Tuple.Create(0, 4000);
        }

        protected virtual bool IsTagAllowed([NotNull] string tag) {
            return tag.Length > 0 && tag.Length < 25;
        }

        [ItemCanBeNull, NotNull]
        protected virtual IEnumerable<string> GetForcedTags() {
            return new string[0];
        }

        protected virtual WorkshopValidatedItem TestTags() {
            var originalTags = new TagsCollection(Target.Tags);
            var newTags = new TagsCollection(originalTags.Where(x => x != null && IsTagAllowed(x.ToLowerInvariant()))
                    .Concat(GetForcedTags()).NonNull()).CleanUp().Sort();
            for (var i = newTags.Count - 1; i >= MaxTagsCount; --i) {
                newTags.RemoveAt(i);
            }

            if (newTags.SequenceEqual(originalTags)) {
                return new WorkshopValidatedItem("Tags are correct");
            }

            return new WorkshopValidatedItem($"Tags will be changed to {newTags.Select(x => $"“{x}”").JoinToString(", ")}",
                    () => Target.Tags = newTags, () => Target.Tags = originalTags);
        }

        protected WorkshopValidatedItem ValidateNumber(string valueName, int? value, int minValue, int maxValue, bool isRequired) {
            if (value.HasValue) {
                return value < minValue || value > maxValue
                        ? new WorkshopValidatedItem($"Incorrect {valueName.ToSentenceMember()}", WorkshopValidatedState.Failed)
                        : new WorkshopValidatedItem($"{valueName} is correct");
            }
            return isRequired
                    ? new WorkshopValidatedItem($"{valueName} is required", WorkshopValidatedState.Failed)
                    : new WorkshopValidatedItem($"{valueName} is not set, but it’s optional");
        }

        protected virtual WorkshopValidatedItem TestYear() {
            var boundaries = GetYearBoundaries();
            return ValidateNumber("Year value", Target.Year, boundaries.Item1, boundaries.Item2, YearRequired);
        }

        public virtual IEnumerable<WorkshopValidatedItem> Validate() {
            yield return TestId();
            yield return TestName();
            if (!IsChildObject) {
                yield return TestVersion();
            }
            yield return TestUrl();
            yield return TestCountry();
            yield return TestDescription();
            yield return TestYear();
            yield return TestTags();
        }
    }
}