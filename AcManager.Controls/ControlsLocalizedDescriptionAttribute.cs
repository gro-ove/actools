using System.ComponentModel;
using System.Globalization;

namespace AcManager.Controls {
    public class ControlsLocalizedDescriptionAttribute : DescriptionAttribute {
        private static string Localize(string key) {
            return ControlsStrings.ResourceManager.GetString(key, CultureInfo.CurrentCulture);
        }

        public ControlsLocalizedDescriptionAttribute([Localizable(false)] string key) : base(Localize(key)) { }
    }
}