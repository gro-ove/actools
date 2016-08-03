using System.ComponentModel;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// Don’t forget to add all strings which should be automatically pluralized here!
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
                case "сервер": return two ? s + "а" : s + "ов";

                case "минута":
                case "трасса":
                case "машина":
                case "погода":
                case "секунда": return two ? s.Substring(0, s.Length - 1) + "ы" : s.Substring(0, s.Length - 1);

                case "день": return two ? "дня" : "дней";
                case "месяц": return two ? "месяца" : "месяцев";
                case "неделя": return two ? "недели" : "недель";
                case "очко": return two ? "очка" : "очков";
                case "ошибка": return two ? "ошибки" : "ошибок";
                case "запись": return two ? "записи" : "записей";
                case "реплей": return two ? "реплея" : "реплеев";
                case "приложение": return two ? "приложения" : "приложений";
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