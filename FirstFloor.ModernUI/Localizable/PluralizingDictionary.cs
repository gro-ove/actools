using System.ComponentModel;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// Don’t forget to add all strings which should be automatically pluralized here!
    /// </summary>
    [Localizable(false)]
    internal static class PluralizingDictionary {
        public static string De(string s) {
            switch (s) {
                case "Reparaturvorschlag:":
                    return "Reparaturvorschläge";

                case "Raum":
                    return "Räume";

                case "Eintrag":
                    return "Einträge";

                case "Runde":
                case "Stunde":
                case "Minute":
                case "Sekunde":
                case "Vorlage":
                case "Strecke":
                case "Woche":
                    return s + "n";

                case "Wiederholung":
                case "Einstellung":
                case "Lackierung":
                case "Schriftart":
                case "Position":
                    return s + "en";

                case "App":
                case "Pitstop":
                case "Showroom":
                case "Setup":
                case "Server":
                    return s + "s";

                case "Tag":
                case "Monat":
                case "Jahr":
                case "Punkt":
                case "Rekord":
                case "Fahrermodel":
                case "Fahrzeug":
                    return s + "e";
            }

            return s; // Gegner, Filter, Wetter all the same
        }

        public static string En(string s) {
            switch (s) {
                case "child":
                    return "children";
                case "person":
                    return "people";
                case "man":
                    return "men";
                case "entry":
                    return "entries";
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

        public static string Pt(string s) {
            if (s.Length == 0) return string.Empty;

            /* http://www.easyportuguese.com/portuguese-lessons/plural/ */

            // Words ending in “ão”: there are 3 alternatives (depeding on the word)
            if (s.EndsWith("ão")) {
                switch (s) {
                    case "avião":
                    case "questão":
                        return s.Substring(0, s.Length - 2) + "ões";
                    case "pão":
                    case "alemão":
                        return s.Substring(0, s.Length - 2) + "ães";
                    case "irmão":
                    case "mão":
                        return s.Substring(0, s.Length - 2) + "ãos";
                }
            }

            // Words ending in “L”: ending in  al, el, ol or ul – change the “l” for “is”
            var lastLetter = s[s.Length - 1];
            if (lastLetter == 'l') {
                return s.Substring(0, s.Length - 1) + "is";
            }

            // Words ending in “m”: change “m” for “ns”
            if (lastLetter == 'm') {
                return s.Substring(0, s.Length - 1) + "ns";
            }

            // Words ending in “r”, “s” or “z”: add – “es”
            if (lastLetter == 'r' || lastLetter == 's' || lastLetter == 'z') {
                return s + "es";
            }

            // Words ending in vowels – add “s”
            if (lastLetter == 'a' || lastLetter == 'e' || lastLetter == 'é' ||
                    lastLetter == 'i' || lastLetter == 'o' || lastLetter == 'u') {
                return s + "s";
            }

            // What to do, what to do?
            return s + "s";
        }

        public static string Ru(string s, bool two) {
            switch (s) {
                case "доступное решение":
                    return "доступные решения";

                case "круг":
                case "оппонент":
                case "противник":
                case "соперник":
                case "повтор":
                case "пит-стоп":
                case "час":
                case "фильтр":
                case "сетап":
                case "скин":
                case "шрифт":
                case "пресет":
                case "шоурум":
                case "сервер":
                case "зал":
                    return two ? s + "а" : s + "ов";

                case "минута":
                case "трасса":
                case "машина":
                case "погода":
                case "секунда":
                    return two ? s.Substring(0, s.Length - 1) + "ы" : s.Substring(0, s.Length - 1);

                case "ошибка":
                case "предустановка":
                    return two ? s.Substring(0, s.Length - 1) + "и" : s.Substring(0, s.Length - 2) + "ок";

                case "день":
                    return two ? "дня" : "дней";
                case "месяц":
                    return two ? "месяца" : "месяцев";
                case "неделя":
                    return two ? "недели" : "недель";
                case "очко":
                    return two ? "очка" : "очков";
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