using System;
using System.Drawing;
using JetBrains.Annotations;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace FirstFloor.ModernUI.Helpers {
    public static class ColorExtension {
        public static string ToHexString(this Color color, bool alphaChannel = false) {
            return $"#{(alphaChannel ? color.A.ToString("X2") : string.Empty)}{color.R.ToString("X2")}{color.G.ToString("X2")}{color.B.ToString("X2")}";
        }

        public static Color ToColor(this System.Drawing.Color color) {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        [CanBeNull]
        public static Color? ToColor(this string s) {
            try {
                return ColorTranslator.FromHtml(s).ToColor();
            } catch (Exception) {
                // TODO
                return null;
            }
        }

        public static double GetHue(this Color c) {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B).GetHue();
        }

        public static double GetBrightness(this Color c) {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B).GetBrightness();
        }

        public static double GetSaturation(this Color c) {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B).GetSaturation();
        }

        /// <summary>
        /// Creates a Color from alpha, hue, saturation and brightness.
        /// </summary>
        /// <param name="alpha">The alpha channel value.</param>
        /// <param name="hue">The hue value.</param>
        /// <param name="saturation">The saturation value.</param>
        /// <param name="brightness">The brightness value.</param>
        /// <returns>A Color with the given values.</returns>
        public static Color FromHsb(double hue, double saturation, double brightness) {
            while (hue < 0) {
                hue += 360;
            }
            while (hue >= 360) {
                hue -= 360;
            }
            double R, G, B;
            if (brightness <= 0) {
                R = G = B = 0;
            } else if (saturation <= 0) {
                R = G = B = brightness;
            } else {
                var hf = hue / 60.0;
                var i = (int)Math.Floor(hf);
                var f = hf - i;
                var pv = brightness * (1 - saturation);
                var qv = brightness * (1 - saturation * f);
                var tv = brightness * (1 - saturation * (1 - f));
                switch (i) {
                    case 0:
                        R = brightness;
                        G = tv;
                        B = pv;
                        break;

                    case 1:
                        R = qv;
                        G = brightness;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = brightness;
                        B = tv;
                        break;

                    case 3:
                        R = pv;
                        G = qv;
                        B = brightness;
                        break;

                    case 4:
                        R = tv;
                        G = pv;
                        B = brightness;
                        break;

                    case 5:
                        R = brightness;
                        G = pv;
                        B = qv;
                        break;

                    case 6:
                        R = brightness;
                        G = tv;
                        B = pv;
                        break;

                    case -1:
                        R = brightness;
                        G = pv;
                        B = qv;
                        break;

                    default:
                        R = G = B = brightness;
                        break;
                }
            }

            return Color.FromRgb(Clamp(R * 255d), Clamp(G * 255d), Clamp(B * 255d));
        }

        private static byte Clamp(double i) {
            return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
        }
    }
}