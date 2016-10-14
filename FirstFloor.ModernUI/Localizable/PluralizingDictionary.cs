using System.ComponentModel;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// Don’t forget to add all strings which should be automatically pluralized here!
    /// </summary>
    [Localizable(false)]
    internal static class PluralizingDictionary {
        public static string En(string s) {
            switch (s) {
                case "child":
                    return "children";
                case "person":
                    return "people";
                case "man":
                    return "men";
            }

            if (s.EndsWith("o")) {
                return null;
            }

            if (s.EndsWith("s") || s.EndsWith("x") || s.EndsWith("ch") || s.EndsWith("sh")) {
                return s + "es";
            }

            return s + "s";
        }

        public static string Es(string s) {
            if (s.Length == 0) return string.Empty;

            /* http://studyspanish.com/grammar/lessons/plnoun */

            // The definite articles (el, la) also change in the plural form. They become “los” and “las.”
            if (s.StartsWith("el ") || s.StartsWith("el ") /* no-break version */) {
                s = "los" + s.Substring(2);
            } else if (s.StartsWith("la ") || s.StartsWith("la ") /* no-break version */) {
                s = "las" + s.Substring(2);
            }

            // If the last letter is an í or ú (with accent), add -es.
            var lastLetter = s[s.Length - 1];
            if (lastLetter == 'í' || lastLetter == 'ú') {
                return s + "s";
            }

            // If a noun ends in a vowel, simply add -s.
            if (lastLetter == 'a' || lastLetter == 'e' || lastLetter == 'é' ||
                    lastLetter == 'i' || lastLetter == 'o' || lastLetter == 'u') {
                return s + "s";
            }

            // If a noun ends in a -z, change the z to c before adding -es.
            if (lastLetter == 'z') {
                return s.Substring(0, s.Length - 1) + "ces";
            }

            // If a noun ends in ión, drop the written accent before adding -es.
            if (s.EndsWith("ión")) {
                return s.Substring(0, s.Length - 3) + "iones";
            }

            // If a noun ends in a consonant, simply add -es.
            return s + "es";
        }

        public static string Ru(string s, bool two) {
            switch (s) {
                case "доступное решение":
                    return "доступные решения";

                case "круг":
                case "оппонент":
                case "противник":
                case "пит-стоп":
                case "час":
                case "фильтр":
                case "сетап":
                case "скин":
                case "шрифт":
                case "пресет":
                case "шоурум":
                case "сервер":
                    return two ? s + "а" : s + "ов";

                case "минута":
                case "трасса":
                case "машина":
                case "погода":
                case "секунда":
                    return two ? s.Substring(0, s.Length - 1) + "ы" : s.Substring(0, s.Length - 1);

                case "день":
                    return two ? "дня" : "дней";
                case "месяц":
                    return two ? "месяца" : "месяцев";
                case "неделя":
                    return two ? "недели" : "недель";
                case "очко":
                    return two ? "очка" : "очков";
                case "ошибка":
                    return two ? "ошибки" : "ошибок";
                case "запись":
                    return two ? "записи" : "записей";
                case "реплей":
                    return two ? "реплея" : "реплеев";
                case "приложение":
                    return two ? "приложения" : "приложений";
            }

            return null;
        }

        public static string RuAlt(string s) {
            switch (s) {
                case "доступное решение":
                    return "доступные решения";
                case "ошибка":
                    return Ru(s, true);
            }

            return null;
        }
    }
}
 