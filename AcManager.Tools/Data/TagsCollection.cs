using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using MoonSharp.Interpreter;

namespace AcManager.Tools.Data {
    [MoonSharpUserData]
    public class TagsCollection : ObservableCollection<string> {
        public TagsCollection() {}

        public TagsCollection([CanBeNull] IEnumerable<string> list) : base(list ?? new string[0]) { }

        public string FirstOrDefault(Func<string, bool> fn) {
            return Enumerable.FirstOrDefault(this, fn);
        }

        public string FirstOrDefault(Closure fn) {
            return Enumerable.FirstOrDefault(this, x => fn.Call(x).Boolean);
        }

        public bool ContainsIgnoringCase(string tag) {
            return this.Any(x => string.Equals(x, tag));
        }

        [NotNull]
        public TagsCollection Sort() {
            return new TagsCollection(this.OrderBy(x => x, TagsComparer.Instance));
        }

        public static IEnumerable<string> CleanUp(IEnumerable<string> tags) {
            return tags.Select(x => {
                var s = x.Trim().ApartFromLast("\"]").ApartFromFirst("[\"").Trim();

                if (Regex.IsMatch(s, @"^#?[abAB]\d\d?$")) {
                    return null;
                }

                if (s[0] == '#') return s;

                var t = s.ToLower();
                switch (t) {
                    case "natural":
                        return null;

                    case "racing":
                        return @"race";

                    case "american":
                        return @"usa";

                    case "road":
                        return @"street";

                    default:
                        return AcStringValues.CountryFromTag(t)?.ToLower() ?? t;
                }
            }).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
        }

        [NotNull]
        public TagsCollection CleanUp() {
            return new TagsCollection(CleanUp(this));
        }
    }
}
