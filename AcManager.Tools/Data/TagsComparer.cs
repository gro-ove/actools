using System;
using System.Collections.Generic;
using AcManager.Tools.Helpers;

namespace AcManager.Tools.Data {
    public class TagsComparer : IComparer<string> {
        public static readonly TagsComparer Instance = new TagsComparer();

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
}