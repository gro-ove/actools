using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace AcTools.Utils {
    public static partial class ImageUtils {
        internal class ColorEntry {
            public double H, S, B;
            public double Smax, Bmax;
            public double Weight;

            public ColorEntry(double h, double s, double b, double w) {
                H = h;
                S = s;
                Smax = s;
                B = b;
                Bmax = b;
                Weight = w;
            }

            public double Distance(double h, double s, double b) {
                var hd = Math.Abs(H - h);
                if (hd > 180) hd = 360 - hd;
                return hd / 360.0 * 3.2 + Math.Abs(S - s) * 0.3 + Math.Abs(B - b) * 0.3;
            }

            public void Add(double h, double s, double b, double w) {
                if (Equals(w, 0d)) return;

                if (h - H >= 180) {
                    h -= 360;
                } else if (H - h >= 180) {
                    H -= 360;
                }

                H = (H * Weight + h * w) / (Weight + w);
                if (H < 0) {
                    H += 360;
                } else if (H >= 360) {
                    H -= 360;
                }

                S = (S * Weight + s * w) / (Weight + w);
                Smax = Math.Max(Smax, s);

                B = (B * Weight + b * w) / (Weight + w);
                Bmax = Math.Max(Bmax, b);
                Weight += w;
            }

            public Color Tune() {
                var result = ToColor(H, Math.Sin(S * Math.PI / 2), (B * 2.0 + Bmax) / 3.0);
                const double fixR = 1.30;
                const double fixG = 1.11;
                const double fixB = 1.00;
                return Color.FromArgb((int)(result.R * fixR).Clamp(0, 255), (int)(result.G * fixG).Clamp(0, 255), (int)(result.B * fixB).Clamp(0, 255));
            }

            public Color Color => ToColor(H, S, B);

            private static Color ToColor(double hue, double sat, double bri) {
                while (hue < 0) {
                    hue += 360;
                }

                while (hue >= 360) {
                    hue -= 360;
                }

                double r, g, b;
                if (bri <= 0) {
                    r = g = b = 0;
                } else if (sat <= 0) {
                    r = g = b = bri;
                } else {
                    var hf = hue / 60.0;
                    var i = (int)Math.Floor(hf);
                    var f = hf - i;
                    var pv = bri * (1 - sat);
                    var qv = bri * (1 - sat * f);
                    var tv = bri * (1 - sat * (1 - f));

                    switch (i) {
                        case 0:
                            r = bri;
                            g = tv;
                            b = pv;
                            break;

                        case 1:
                            r = qv;
                            g = bri;
                            b = pv;
                            break;

                        case 2:
                            r = pv;
                            g = bri;
                            b = tv;
                            break;

                        case 3:
                            r = pv;
                            g = qv;
                            b = bri;
                            break;

                        case 4:
                            r = tv;
                            g = pv;
                            b = bri;
                            break;

                        case 5:
                            r = bri;
                            g = pv;
                            b = qv;
                            break;

                        case 6:
                            r = bri;
                            g = tv;
                            b = pv;
                            break;

                        case -1:
                            r = bri;
                            g = pv;
                            b = qv;
                            break;

                        default:
                            r = g = b = bri;
                            break;
                    }
                }

                return Color.FromArgb((int)(r.Saturate() * 255d), (int)(g.Saturate() * 255d), (int)(b.Saturate() * 255d));
            }
        }

        private const int Size = 200;
        private const int Padding = 40;
        private const double Threshold = 0.2;

        public static Color[] GetBaseColors(Bitmap bitmap) {
            if (bitmap.Width == Size && bitmap.Height == Size) {
                return GetBaseColors48(bitmap);
            }

            using (var resized = new Bitmap(Size, Size))
            using (var graphics = Graphics.FromImage(resized)) {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(bitmap, 0, 0, Size, Size);
                return GetBaseColors48(resized);
            }
        }

        public static Color[] GetBaseColors48(Bitmap bitmap) {
            var colors = new List<ColorEntry>(50);

            for (var y = Padding; y < Size - Padding; y++) {
                for (var x = Padding; x < Size - Padding; x++) {
                    var c = bitmap.GetPixel(x, y);
                    var h = c.GetHue();
                    var s = c.GetSaturation();
                    var b = c.GetBrightness();

                    var w = (1.2 - Math.Abs((double)x / Size - 0.5) - Math.Abs((double)y / Size - 0.5)).Saturate();
                    if (b < 0.1) continue;

                    w += s;

                    for (var i = 0; i < colors.Count; i++) {
                        var en = colors[i];
                        if (en.Distance(h, s, b) > Threshold) continue;

                        en.Add(h, s, b, w);
                        for (var j = i + 1; j < colors.Count; j++) {
                            var em = colors[j];
                            if (em.Distance(en.H, en.S, en.B) > Threshold) continue;

                            en.Add(em.H, em.S, em.B, em.Weight);
                            colors.Remove(em);
                            j--;
                        }

                        goto loop;
                    }

                    colors.Add(new ColorEntry(h, s, b, w));
                    loop:;
                }
            }

            if (colors.Count == 0) return new Color[0];

            var result = colors.OrderBy(x => -x.Weight).ToList();
            return result.Where(x => x.Weight > result.ElementAt(0).Weight * 0.1).Select(x => x.Tune()).ToArray();
        }

        [Obsolete]
        public static void CreateLivery(string outputFile, params Color[] color) {
            using (var bitmap = new Bitmap(64, 64))
            using (var graphics = Graphics.FromImage(bitmap)) {
                var max = color.Length > 15 ? 15 : color.Length;
                for (var i = 0; i < max; i++) {
                    graphics.FillRectangle(new SolidBrush(color[i]), 0, (float)Math.Sqrt((double)i / max) * 64, 64, 64);
                }

                bitmap.Save(outputFile, ImageFormat.Png);
            }
        }

        [Obsolete]
        public static void GenerateLivery(string inputFile, string outputFile) {
            using (var image = Image.FromFile(inputFile)) {
                CreateLivery(outputFile, GetBaseColors((Bitmap)image));
            }
        }
    }
}
