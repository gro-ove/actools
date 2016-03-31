using System.Text.RegularExpressions;
using System.Globalization;
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
            return int.Parse(result.Value);
        }

        private static Regex _nameYearRegex;

        private static Regex NameYearRegex => _nameYearRegex ??
                                              (_nameYearRegex = new Regex(@"\s(?:'(\d\d)|'?((?:19[2-9]|20[01])\d))$", RegexOptions.Compiled));

        public static int? GetYearFromName([NotNull] string name) {
            var result = NameYearRegex.Match(name);
            if (!result.Success) return null;

            var matched = result.Groups[1].Value;
            if (string.IsNullOrEmpty(matched)) matched = result.Groups[2].Value;
            var value = int.Parse(matched);
            return value < 1000 ? value < 18 ? 2000 + value : 1900 + value : value;
        }

        public static bool NameContainsYear([NotNull] string name, int? year = null) {
            var value = GetYearFromName(name);
            return value.HasValue && (!year.HasValue || value.Value == year.Value);
        }

        public static string NameReplaceYear([NotNull] string name, int year) {
            var digits = (year % 100).ToString(CultureInfo.InvariantCulture);
            return NameYearRegex.Replace(name, digits.Length == 2 ? " '" + digits : " '0" + digits, 1);
        }

        public static string CountryFromBrand([NotNull] string brand) {
            var key = brand.Trim().ToLower();
            return DataProvider.Instance.BrandCountries.GetValueOrDefault(key);
        }

        public static string CountryFromTag([NotNull] string tag) {
            var key = tag.Trim().ToLower();
            return DataProvider.Instance.TagCountries.GetValueOrDefault(key) ?? DataProvider.Instance.Countries.GetValueOrDefault(key);
        }

        public static string NameFromId([NotNull] string id) {
            id = Regex.Replace(id, @"[\s_]+|(?<=[a-z])-?(?=[A-Z\d])", " ");
            id = Regex.Replace(id, @"\b[a-z]", x => x.Value.ToUpper());
            return id;
        }

        public static string IdFromName([NotNull] string name) {
            name = Regex.Replace(name, @"\W+", "_");
            return name.ToLower();
        }

        private static Regex _decodeDescriptionRegex;
        private static Regex _cleanDescriptionRegex;

        public static string DecodeDescription(string s) {
            if (s == null) return null;
            s = (_decodeDescriptionRegex ?? (_decodeDescriptionRegex = new Regex(@"<\s*/?\s*br\s*/?\s*>\s*", RegexOptions.Compiled))).Replace(s, "\n");
            s = (_cleanDescriptionRegex ?? (_cleanDescriptionRegex = new Regex(@"<\s*\w+(\s+[^>]*|\s*)>|</\s*\w+\s*>", RegexOptions.Compiled))).Replace(s, "");
            return s;
        }

        public static string EncodeDescription(string s) {
            return s?.Replace("\r", "").Replace("\n", "<br>");
        }
    }
}
