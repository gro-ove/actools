using System.ComponentModel;
using System.Globalization;

namespace AcManager.Tools {
    public class LocalizedDescriptionAttribute : DescriptionAttribute {
        private static string Localize(string key) {
            return ToolsStrings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        }

        public LocalizedDescriptionAttribute([Localizable(false)] string key) : base(Localize(key) ?? key) {}

#if DEBUG
        internal class Temp {
            private string _a = ToolsStrings.PlaceConditionType_Points;
        }
#endif
    }
}