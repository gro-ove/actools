using System;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Linq;
using AcManager.Tools.Data;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {

    public static class AcStringValues {
        private static Regex _idYearRegex;

        private static Regex IdYearRegex => _idYearRegex ??
                                            (_idYearRegex = new Regex(@"(?:19[2-9]|20[01])\d$", RegexOptions.Compiled));

        public static int? GetYearFromId([NotNull] string id) {
            var result = IdYearRegex.Match(id);
            if (!result.Success) return null;
            return int.Parse(result.Value, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        private static Regex _nameYearRegex;

        private static Regex NameYearRegex => _nameYearRegex ??
                                              (_nameYearRegex = new Regex(@"\s(?:'(\d\d)|'?((?:19[2-9]|20[01])\d))$", RegexOptions.Compiled));

        public static int? GetYearFromName([NotNull] string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var result = NameYearRegex.Match(name);
            if (!result.Success) return null;

            var matched = result.Groups[1].Value;
            if (string.IsNullOrEmpty(matched)) matched = result.Groups[2].Value;
            var value = int.Parse(matched, NumberStyles.Any, CultureInfo.InvariantCulture);
            return value < 1000 ? value < 18 ? 2000 + value : 1900 + value : value;
        }

        public static bool NameContainsYear([NotNull] string name, int? year = null) {
            var value = GetYearFromName(name);
            return value.HasValue && (!year.HasValue || value.Value == year.Value);
        }

        [NotNull]
        public static string NameReplaceYear([NotNull] string name, int year) {
            var digits = (year % 100).ToString(CultureInfo.InvariantCulture);
            return NameYearRegex.Replace(name, digits.Length == 2 ? " '" + digits : " '0" + digits, 1);
        }

        private static Regex _nameVersionRegex;

        private static Regex NameVersionRegex => _nameVersionRegex ??
                                              (_nameVersionRegex = new Regex(@"^(.+?) (?:[vV]?(\d+\.\d[\w\.]*))$", RegexOptions.Compiled));

        public static string GetVersionFromName([NotNull] string name, out string nameWithoutVersion) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            var match = NameVersionRegex.Match(name);
            if (match.Success) {
                nameWithoutVersion = match.Groups[1].Value;
                return match.Groups[2].Value;
            }

            nameWithoutVersion = name;
            return null;
        }

        [CanBeNull]
        public static string BrandFromName([NotNull] string name) {
            var key = name.Trim().ToLower();
            return (from brand in DataProvider.Instance.BrandCountries.Keys
                    where key.StartsWith(brand) && (char.IsWhiteSpace(key.ElementAtOrDefault(brand.Length)) || key.ElementAtOrDefault(brand.Length) == '_')
                    select name.Trim().Substring(0, brand.Length)).FirstOrDefault();
        }

        [CanBeNull]
        public static string CountryFromBrand([NotNull] string brand) {
            var key = brand.Trim().ToLower();
            return DataProvider.Instance.BrandCountries.GetValueOrDefault(key);
        }

        [CanBeNull]
        public static string CountryFromTag([NotNull] string tag) {
            var key = tag.Trim().ToLower();
            return DataProvider.Instance.TagCountries.GetValueOrDefault(key) ?? DataProvider.Instance.Countries.GetValueOrDefault(key);
        }

        [NotNull]
        public static string NameFromId([NotNull] string id) {
            id = Regex.Replace(id, @"[\s_]+|(?<=[a-z])-?(?=[A-Z\d])", " ");
            id = Regex.Replace(id, @"\b[a-z]", x => x.Value.ToUpper());
            return id;
        }

        [NotNull]
        public static string IdFromName([NotNull] string name) {
            name = Regex.Replace(name, @"\W+", "_");
            return name.ToLower();
        }

        private static Regex _decodeDescriptionRegex;
        private static Regex _cleanDescriptionRegex;

        [CanBeNull]
        public static string DecodeDescription([CanBeNull] string s) {
            if (s == null) return null;
            s = (_decodeDescriptionRegex ?? (_decodeDescriptionRegex = new Regex(@"<\s*/?\s*br\s*/?\s*>\s*", RegexOptions.Compiled))).Replace(s, "\n");
            s = (_cleanDescriptionRegex ?? (_cleanDescriptionRegex = new Regex(@"<\s*\w+(\s+[^>]*|\s*)>|</\s*\w+\s*>", RegexOptions.Compiled))).Replace(s, "");
            return s;
        }

        [CanBeNull]
        public static string EncodeDescription([CanBeNull] string s) {
            return s?.Replace("\r", "").Replace("\n", "<br>");
        }

        public static bool IsAppropriateId([CanBeNull] string id) {
            return id != null && Regex.IsMatch(id, @"^\w[\w-]*$");
        }
    }
}
