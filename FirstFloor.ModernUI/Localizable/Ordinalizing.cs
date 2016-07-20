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
                return string.Format(Resources.Ordinalizing_Minus, Base(-v).ToLowerInvariant());
            }

            switch (v) {
                case 0:
                    return Resources.Ordinalizing_Zeroth;
                case 1:
                    return Resources.Ordinalizing_First;
                case 2:
                    return Resources.Ordinalizing_Second;
                case 3:
                    return Resources.Ordinalizing_Third;
                case 4:
                    return Resources.Ordinalizing_Fourth;
                case 5:
                    return Resources.Ordinalizing_Fifth;
                case 6:
                    return Resources.Ordinalizing_Sixth;
                case 7:
                    return Resources.Ordinalizing_Seventh;
                case 8:
                    return Resources.Ordinalizing_Eighth;
                case 9:
                    return Resources.Ordinalizing_Ninth;
                case 10:
                    return Resources.Ordinalizing_Tenth;
                case 11:
                    return Resources.Ordinalizing_Eleventh;
                case 12:
                    return Resources.Ordinalizing_Twelfth;
                case 13:
                    return Resources.Ordinalizing_Thirteenth;
                case 14:
                    return Resources.Ordinalizing_Fourteenth;
                case 15:
                    return Resources.Ordinalizing_Fifteenth;
                case 16:
                    return Resources.Ordinalizing_Sixteenth;
                case 17:
                    return Resources.Ordinalizing_Seventeenth;
                case 18:
                    return Resources.Ordinalizing_Eighteenth;
                case 19:
                    return Resources.Ordinalizing_Nineteenth;
                case 20:
                    return Resources.Ordinalizing_Twentieth;
                case 21:
                    return Resources.Ordinalizing_TwentyFirst;
                case 22:
                    return Resources.Ordinalizing_TwentySecond;
                case 23:
                    return Resources.Ordinalizing_TwentyThird;
                case 24:
                    return Resources.Ordinalizing_TwentyFourth;
                case 25:
                    return Resources.Ordinalizing_TwentyFifth;
                case 26:
                    return Resources.Ordinalizing_TwentySixth;
                case 27:
                    return Resources.Ordinalizing_TwentySeventh;
                case 28:
                    return Resources.Ordinalizing_TwentyEighth;
                case 29:
                    return Resources.Ordinalizing_TwentyNinth;
                case 30:
                    return Resources.Ordinalizing_Thirtieth;
                case 31:
                    return Resources.Ordinalizing_ThirtyFirst;
                default:
                    return string.Format(Resources.Ordinalizing_Nth, v);
            }
        }

        public static string Convert(int v, string s, CultureInfo culture = null) {
            string result;
            switch ((culture ?? CultureInfo.CurrentUICulture).Name.ToLowerInvariant()) {
                case "ru":
                case "ru-ru":
                    result = Base(v);
                    break;
                default:
                    result = Base(v);
                    break;
            }

            return result == @"-" ? string.Format(Resources.Ordinalizing_Nth, v) : result;
        }
    }
}