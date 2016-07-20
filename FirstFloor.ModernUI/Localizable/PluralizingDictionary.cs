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

            if (s.EndsWith("y")) {
                return s.Substring(0, s.Length - 1) + "ies";
            }

            return s + "s";
        }
        
        public static string Ru(string s, bool two) {
            switch (s) {
                case "день": return two ? "дн€" : "дней";
                case "час": return two ? "часа" : "часов";
                case "минута": return two ? "минуты" : "минут";
                case "секунда": return two ? "секунды" : "секунд";
                case "круг": return two ? "круга" : "кругов";
                case "оппонент": return two ? "оппонента" : "оппонентов";
                case "противник": return two ? "противника" : "противников";
                case "пит-стоп": return two ? "пит-стопа" : "пит-стопов";
                case "сервер": return two ? "сервера" : "серверов";
            }

            return null;
        }
    }
}