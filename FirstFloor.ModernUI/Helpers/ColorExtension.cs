using System;
using System.Drawing;
using JetBrains.Annotations;
using Color = System.Windows.Media.Color;

namespace FirstFloor.ModernUI.Helpers {
    public static class ColorExtension {
        public static string ToHexString(this Color color, bool alphaChannel = false) {
            return $"#{(alphaChannel ? color.A.ToString("X2") : string.Empty)}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        public static Color ToColor(this System.Drawing.Color color) {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Drawing.Color ToColor(this Color color) {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static System.Drawing.Color ToColor(this Color color, int alpha) {
            return System.Drawing.Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        [CanBeNull]
        public static Color? ToColor(this string s) {
            if (s == null) return null;
            try {
                return ColorTranslator.FromHtml(s).ToColor();
            } catch (Exception) {
                return null;
            }
        }

        public static double GetHue(this Color c) {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B).GetHue();
        }

        public static double GetBrightness(this Color c) {
            return Math.Max(c.R, Math.Max(c.G, c.B)) / 255d;
        }

        public static double GetSaturation(this Color c) {
            int max = Math.Max(c.R, Math.Max(c.G, c.B));
            return max == 0 ? 0 : 1d - 1d * Math.Min(c.R, Math.Min(c.G, c.B)) / max;
        }

        public static Color FromHsb(double hue, double saturation, double value) {
            var f = hue / 60 - Math.Floor(hue / 60);
            var v = Clamp(255 * value);
            var p = Clamp(255 * value * (1 - saturation));
            var q = Clamp(255 * value * (1 - f * saturation));
            var t = Clamp(255 * value * (1 - (1 - f) * saturation));
            switch ((int)Math.Floor(hue / 60) % 6) {
                case 0:
                    return Color.FromRgb(v, t, p);
                case 1:
                    return Color.FromRgb(q, v, p);
                case 2:
                    return Color.FromRgb(p, v, t);
                case 3:
                    return Color.FromRgb(p, q, v);
                case 4:
                    return Color.FromRgb(t, p, v);
                default:
                    return Color.FromRgb(v, p, q);
            }
        }

        private static byte Clamp(double i) {
            return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
        }
    }
}