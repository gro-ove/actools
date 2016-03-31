using System.Globalization;
using System.Text.RegularExpressions;

namespace StringBasedFilter.TestEntries {
    internal class RegexTestEntry : ITestEntry {
        private readonly Regex _regex;

        public RegexTestEntry(Regex regex) {
            _regex = regex;
        }

        public override string ToString() {
            return "~" + _regex;
        }

        public bool Test(string value) {
            return value != null && _regex.IsMatch(value);
        }

        public bool Test(double value) {
            return _regex.IsMatch(value.ToString(CultureInfo.InvariantCulture));
        }

        public bool Test(bool value) {
            return _regex.IsMatch(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
