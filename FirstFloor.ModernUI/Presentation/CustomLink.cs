using System;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Presentation {
    /// <summary>
    /// Will be saved and loaded.
    /// </summary>
    public class CustomLink : Link {
        public string Serialize() {
            return Storage.Encode(DisplayName) + '\n' + Storage.Encode(Source?.ToString() ?? string.Empty);
        }

        public static CustomLink Deserialize(string s) {
            try {
                var p = s.Split(new[] { '\n' }, 2);
                return new CustomLink {
                    DisplayName = Storage.Decode(p[0]),
                    Source = new Uri(p[1], UriKind.RelativeOrAbsolute)
                };
            } catch (Exception e) {
                Logging.Warning("Can’t deserialize custom link: " + e);
                return null;
            }
        }
    }
}