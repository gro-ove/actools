using System.ComponentModel;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// DonТt forget to add all strings which should be automatically pluralized here!
    /// </summary>
    [Localizable(false)]
    internal static class PluralizingDictionary {
        public static string En(string s) {
            switch (s) {
                case "child": return "children";
                case "person": return "people";
                case "man": return "men";
            }

            if (s.EndsWith("o")) {
                return null;
            }

            if (s.EndsWith("s") || s.EndsWith("x") || s.EndsWith("ch") || s.EndsWith("sh")) {
                return s + "es";
            }

            return s + "s";
        }
        
        public static string Ru(string s, bool two) {
            switch (s) {
                case "доступное решение":
                    return "доступные решени€";

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
                case "сервер": return two ? s + "а" : s + "ов";

                case "минута":
                case "трасса":
                case "машина":
                case "погода":
                case "секунда": return two ? s.Substring(0, s.Length - 1) + "ы" : s.Substring(0, s.Length - 1);

                case "день": return two ? "дн€" : "дней";
                case "очко": return two ? "очка" : "очков";
                case "ошибка": return two ? "ошибки" : "ошибок";
                case "запись": return two ? "записи" : "записей";
                case "реплей": return two ? "репле€" : "реплеев";
                case "приложение": return two ? "приложени€" : "приложений";
            }

            return null;
        }

        public static string RuAlt(string s) {
            switch (s) {
                case "доступное решение":
                    return "доступные решени€";
                case "ошибка":
                    return Ru(s, true);
            }

            return null;
        }
    }
}