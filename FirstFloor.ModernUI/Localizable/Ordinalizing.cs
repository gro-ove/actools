using System.Globalization;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// Returns ordinal string for each number. Usually works from resources, but if you want
    /// some specific form (for example, for Russian: “ïåðâûé” — “ïåðâàÿ”), use this file.
    /// </summary>
    internal static class Ordinalizing {
        /// <summary>
        /// Base version, takes strings from resources. Doesn’t consider different genders, 
        /// forms and everything.
        /// </summary>
        /// <param name="v">Value</param>
        /// <returns></returns>
        private static string Base(int v) {
            if (v < 0) {
                return string.Format(UiStrings.Ordinalizing_Minus, Base(-v).ToLowerInvariant());
            }

            switch (v) {
                case 0:
                    return UiStrings.Ordinalizing_Zeroth;
                case 1:
                    return UiStrings.Ordinalizing_First;
                case 2:
                    return UiStrings.Ordinalizing_Second;
                case 3:
                    return UiStrings.Ordinalizing_Third;
                case 4:
                    return UiStrings.Ordinalizing_Fourth;
                case 5:
                    return UiStrings.Ordinalizing_Fifth;
                case 6:
                    return UiStrings.Ordinalizing_Sixth;
                case 7:
                    return UiStrings.Ordinalizing_Seventh;
                case 8:
                    return UiStrings.Ordinalizing_Eighth;
                case 9:
                    return UiStrings.Ordinalizing_Ninth;
                case 10:
                    return UiStrings.Ordinalizing_Tenth;
                case 11:
                    return UiStrings.Ordinalizing_Eleventh;
                case 12:
                    return UiStrings.Ordinalizing_Twelfth;
                case 13:
                    return UiStrings.Ordinalizing_Thirteenth;
                case 14:
                    return UiStrings.Ordinalizing_Fourteenth;
                case 15:
                    return UiStrings.Ordinalizing_Fifteenth;
                case 16:
                    return UiStrings.Ordinalizing_Sixteenth;
                case 17:
                    return UiStrings.Ordinalizing_Seventeenth;
                case 18:
                    return UiStrings.Ordinalizing_Eighteenth;
                case 19:
                    return UiStrings.Ordinalizing_Nineteenth;
                case 20:
                    return UiStrings.Ordinalizing_Twentieth;
                case 21:
                    return UiStrings.Ordinalizing_TwentyFirst;
                case 22:
                    return UiStrings.Ordinalizing_TwentySecond;
                case 23:
                    return UiStrings.Ordinalizing_TwentyThird;
                case 24:
                    return UiStrings.Ordinalizing_TwentyFourth;
                case 25:
                    return UiStrings.Ordinalizing_TwentyFifth;
                case 26:
                    return UiStrings.Ordinalizing_TwentySixth;
                case 27:
                    return UiStrings.Ordinalizing_TwentySeventh;
                case 28:
                    return UiStrings.Ordinalizing_TwentyEighth;
                case 29:
                    return UiStrings.Ordinalizing_TwentyNinth;
                case 30:
                    return UiStrings.Ordinalizing_Thirtieth;
                case 31:
                    return UiStrings.Ordinalizing_ThirtyFirst;
                default:
                    return string.Format(UiStrings.Ordinalizing_Nth, v);
            }
        }

        public static string Convert(int v, string s) {
            string result;
            switch (CultureInfo.CurrentUICulture.Name.ToLowerInvariant()) {
                case "ru":
                case "ru-ru":
                    result = Base(v);
                    break;
                default:
                    result = Base(v);
                    break;
            }

            return result == @"-" ? string.Format(UiStrings.Ordinalizing_Nth, v) : result;
        }
    }
}