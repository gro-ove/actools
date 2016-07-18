using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using JetBrains.Annotations;
using MoonSharp.Interpreter;

namespace AcManager.Tools.Data {
    public class TagsComparer : IComparer<string> {
        public int Compare(string x, string y) {
            var categoryX = GetCategory(x);
            var categoryY = GetCategory(y);
            if (categoryX != categoryY) return categoryX - categoryY;
            return string.Compare(x, y, StringComparison.Ordinal);
        }

        private static int GetCategory(string s) {
            if (s[0] == '#') {
                return 0;
            }

            switch (s.ToLower()) {
                case "4wd":
                case "awd":
                case "fwd":
                case "rwd":
                    return 10;
                    
                case "automatic":
                case "manual":
                case "semiautomatic":
                case "sequential":
                    return 20;

                case "h-shifter":
                    return 21;
                    
                case "compressor":
                case "turbo":
                case "v4":
                case "v6":
                case "v8":
                case "v10":
                case "v12":
                    return 25;

                case "lightweight":
                case "heavyweight":
                case "subcompact":
                    return 28;
                    
                case "drift":
                case "rally":
                case "race":
                case "racing":
                case "street":
                case "sportscar":
                case "trackday":
                case "wrc":
                    return 30;

                default:
                    return AcStringValues.CountryFromTag(s) != null ? 100 : 50;
            }
        }
    }

    [MoonSharpUserData]
    public class TagsCollection : ObservableCollection<string> {
        public TagsCollection() {}

        public TagsCollection(IEnumerable<string> list) : base(list) { }

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
            return new TagsCollection(this.OrderBy(x => x, new TagsComparer()));
        }

        [NotNull]
        public TagsCollection CleanUp() {
            // TODO: Special case for tracks?
            return new TagsCollection(this.Select(x => {
                var s = x.Trim();

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
            }).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        }
    }
}
