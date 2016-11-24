using System.ComponentModel;
using System.Globalization;

namespace AcManager.Tools {
    internal class LocalizedDescriptionAttribute : DescriptionAttribute {
        private static string Localize(string key) {
            return ToolsStrings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        }

        public LocalizedDescriptionAttribute([Localizable(false)] string key) : base(Localize(key) ?? key) { }
    }
}