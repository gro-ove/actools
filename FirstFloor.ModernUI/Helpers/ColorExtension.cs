using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class ColorExtension {
        public static string ToHexString(this Color color, bool alphaChannel = false) {
            return $"#{(alphaChannel ? color.A.ToString("X2") : string.Empty)}{color.R.ToString("X2")}{color.G.ToString("X2")}{color.B.ToString("X2")}";
        }

        [CanBeNull]
        public static Color? ToColor(this string s) {
            return ColorConverter.ConvertFromString(s) as Color?;
        }
    }
}