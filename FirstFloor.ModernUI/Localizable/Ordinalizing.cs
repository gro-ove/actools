using System;
using System.ComponentModel;
using System.Globalization;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// Returns ordinal string for each number. Usually works from resources, but if you want
    /// some specific form (for example, for Russian: вЂњРїРµСЂРІС‹Р№вЂќ вЂ” вЂњРїРµСЂРІР°СЏвЂќ), use this file.
    /// </summary>
    [Localizable(false)]
    internal static class Ordinalizing {
        private const string FallbackToShort = "-";

        #region Languages-specific
        #region English
        private static string EnPostfix(int v, string s) {
            if (v >= 10 && v <= 20) return "th";
            switch (v % 10) {
                case 1:
                    return "st";
                case 2:
                    return "nd";
                case 3:
                    return "rd";
                default:
                    return "th";
            }
        }

        private static string EnLong(int v, string s) {
            return BaseLong(v);
        }
        #endregion

        #region Russian
        private enum RuGenger {
            Masculine, Feminine, Neuter
        }

        private static RuGenger RuGetGenger(string s) {
            switch (s?.ToLower(CultureInfo.CurrentUICulture)) {
                case "место":
                    return RuGenger.Neuter;
            }

            return RuGenger.Masculine;
        }

        private static string RuPostfix(int v, string s) {
            /* http://ilyabirman.ru/meanwhile/all/o-naraschenii-okonchaniy-chislitelnyh/ */
            switch (RuGetGenger(s)) {
                case RuGenger.Masculine:
                    return "-й";
                case RuGenger.Feminine:
                    return "-я";
                case RuGenger.Neuter:
                    return "-е";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string RuLong(int v, string s) {
            switch (RuGetGenger(s)) {
                case RuGenger.Masculine:
                    return BaseLong(v);
                case RuGenger.Feminine:
                    return FallbackToShort;
                case RuGenger.Neuter:
                    return FallbackToShort;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion

        #region Spanish
        private enum EsGenger {
            Default, Feminine, NounMasculine, PreMasculine
        }

        private static EsGenger EsGetGenger(string s) {
#if DEBUG
            Logging.Debug("gender: " + s);
#endif
            if (string.IsNullOrEmpty(s)) return EsGenger.Default;

            var lower = s.ToLower(CultureInfo.CurrentUICulture);
            switch (lower) {
                case "coche":
                    return EsGenger.PreMasculine;
                case "foto":
                case "moto":
                case "mujer":
                    return EsGenger.Feminine;
            }

            var lastCharacter = lower[lower.Length - 1];
            switch (lastCharacter) {
                case 'a':
                    return EsGenger.Feminine;
                case 'o':
                    return EsGenger.NounMasculine;
            }

            if (lower.EndsWith("sión") || lower.EndsWith("ción") || lower.EndsWith("gión") ||
                    lower.EndsWith("ez") || lower.EndsWith("triz") ||lower.EndsWith("umbre") ||
                    lower.EndsWith("dad") || lower.EndsWith("tad") || lower.EndsWith("tud")) {
                return EsGenger.Feminine;
            }

            if (lower.EndsWith("ma") || lower.EndsWith("ta") || lower.EndsWith("pa")) {
                return EsGenger.NounMasculine;
            }

            return EsGenger.Default;
        }

        private static string EsPostfix(int v, string s) {
            var g = EsGetGenger(s);

            if (v == 1) {
                switch (g) {
                    case EsGenger.Default:
                        return ".º";
                    case EsGenger.Feminine:
                        return ".ᵉʳᵃ";
                    case EsGenger.NounMasculine:
                        return ".ᵉʳᵒ";
                    case EsGenger.PreMasculine:
                        return ".ᵉʳ";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            switch (g) {
                case EsGenger.Default:
                    return ".º";
                case EsGenger.Feminine:
                    return ".ª";
                case EsGenger.NounMasculine:
                    return ".º";
                case EsGenger.PreMasculine:
                    return ".º";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string EsLong(int v, string s) {
            return BaseLong(v);
        }
        #endregion

        #region Chinese (Simplified)
        private static string ZhCnPostfix(int v, string s) {
            switch (s) {
                case "车手":
                    return "名";
                default:
                    return "";
            }
        }

        private static string ZhCnLong(int v, string s) {
            return BaseLong(v);
        }
        #endregion
        #endregion

        /// <summary>
        /// Short version: “1” → “1st”, “2” → “2nd”, …
        /// </summary>
        /// <param name="v">Integer.</param>
        /// <param name="s">Subject string (for languages in which result might depend on a gender or something like that).</param>
        /// <returns>Localized string</returns>
        public static string ConvertShort(int v, string s) {
            var culture = CultureInfo.CurrentUICulture;
            if (culture.Name.Length < 2) return string.Empty;
            switch (culture.Name.Substring(0, 2).ToLowerInvariant()) {
                case "en":
                    return $"{v}{EnPostfix(Math.Abs(v), s)}";
                case "es":
                    return $"{v}{EsPostfix(Math.Abs(v), s)}";
                case "ru":
                    return $"{v}{RuPostfix(Math.Abs(v), s)}";
                case "zh":
                    return $"第{v}{ZhCnPostfix(Math.Abs(v), s)}";
                default:
                    return v.ToString(culture);
            }

        }

        /// <summary>
        /// Base version, takes strings from resources. Doesn’t consider different genders, 
        /// forms and everything.
        /// </summary>
        /// <param name="v">Value</param>
        /// <returns></returns>
        private static string BaseLong(int v) {
            if (v < 0) {
                return string.Format(UiStrings.Ordinalizing_Minus, BaseLong(-v).ToLowerInvariant());
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

        /// <summary>
        /// Long version: “1” → “First”, “2” → “Second”, …
        /// </summary>
        /// <param name="v">Integer.</param>
        /// <param name="s">Subject string (for languages in which result might depend on a gender or something like that).</param>
        /// <returns>Localized string</returns>
        public static string ConvertLong(int v, string s) {
            string result;
            var culture = CultureInfo.CurrentUICulture;
            if (culture.Name.Length < 2) return s;
            switch (culture.Name.Substring(0, 2).ToLowerInvariant()) {
                case "en":
                    result = EnLong(v, s);
                    break;
                case "es":
                    result = EsLong(v, s);
                    break;
                case "ru":
                    result = RuLong(v, s);
                    break;
                case "zh":
                    result = ZhCnLong(v, s);
                    break;
                default:
                    result = BaseLong(v);
                    break;
            }

            return result == FallbackToShort ? ConvertShort(v, s) : result;
        }
    }
}