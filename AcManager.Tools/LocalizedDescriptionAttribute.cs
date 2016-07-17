using System.ComponentModel;

namespace AcManager.Tools {
    public class LocalizedDescriptionAttribute : DescriptionAttribute {
        private static string Localize(string key) {
            return Resources.ResourceManager.GetString(key);
        }

        public LocalizedDescriptionAttribute([Localizable(false)] string key) : base(Localize(key)) {}
    }
}