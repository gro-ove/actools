using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace AcTools.Utils {
    public static partial class ImageUtils {
        internal class ColorEntry {
            public double H, S, V;
            public double Weight;

            public double Distance(double h, double s, double v) {
                var hd = Math.Abs(H - h);
                if (hd > 180) hd = 360 - hd;
                return (hd / 360) * 2.0 + Math.Abs(S - s) * 0.7 + Math.Abs(V - v) * 0.9;
            }

            public void Add(double h, double s, double v, double w) {
                H = Math.Sqrt((H * H * Weight + h * h * w) / (Weight + w));
                S = Math.Sqrt((S * S * Weight + s * s * w) / (Weight + w));
                V = Math.Sqrt((V * V * Weight + v * v * w) / (Weight + w));
                Weight += w;
            }

            public Color Tune() {
                S = (S * 1.1).Saturate();
                V = (V * 1.1).Saturate();
                return Color;
            }

            public Color Color {
                get {
                    var hi = Convert.ToInt32(Math.Floor(H / 60)) % 6;
                    var f = H / 60 - Math.Floor(H / 60);

                    V = V * 255;
                    var v = Convert.ToInt32(V);
                    var p = Convert.ToInt32(V * (1 - S));
                    var q = Convert.ToInt32(V * (1 - f * S));
                    var t = Convert.ToInt32(V * (1 - (1 - f) * S));

                    switch (hi) {
                        case 0:
                            return Color.FromArgb(255, v, t, p);
                        case 1:
                            return Color.FromArgb(255, q, v, p);
                        case 2:
                            return Color.FromArgb(255, p, v, t);
                        case 3:
                            return Color.FromArgb(255, p, q, v);
                        case 4:
                            return Color.FromArgb(255, t, p, v);
                        default:
                            return Color.FromArgb(255, v, p, q);
                    }
                }
            }
        }

        private static void ColorToHsv(Color color, out double hue, out double saturation, out double value) {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = max == 0 ? 0 : 1d - 1d * min / max;
            value = max / 255d;
        }

        private const int Size = 48;

        public static Color[] GetBaseColors(Bitmap bitmap) {
            if (bitmap.Width == Size && bitmap.Height == Size) {
                return GetBaseColors48(bitmap);
            }
            
            using (var resized = new Bitmap(bitmap, new Size(Size, Size))) {
                return GetBaseColors48(resized);
            }
        }

        public static Color[] GetBaseColors48(Bitmap bitmap) {
            const int skip = 4;
            const double threshold = 0.15;

            var colors = new List<ColorEntry>(50);

            for (var y = skip; y < Size - skip; y++) {
                for (var x = skip; x < Size - skip; x++) {
                    var color = bitmap.GetPixel(x, y);

                    double h, s, v;
                    ColorToHsv(color, out h, out s, out v);

                    var w = (1.2 - Math.Abs((double)x / Size - 0.5) - Math.Abs((double)y / Size - 0.5)).Saturate();
                    w *= v * s;

                    for (var i = 0; i < colors.Count; i++) {
                        var en = colors[i];
                        if (en.Distance(h, s, v) > threshold) continue;

                        en.Add(h, s, v, w);
                        for (var j = i + 1; j < colors.Count; j++) {
                            var em = colors[j];
                            if (em.Distance(en.H, en.S, en.V) > threshold) continue;

                            en.Add(em.H, em.S, em.V, em.Weight);
                            colors.Remove(em);
                            j--;
                        }

                        goto loop;
                    }

                    colors.Add(new ColorEntry {
                        H = h,
                        S = s,
                        V = v,
                        Weight = w
                    });

                    loop:;
                }
            }

            var result = colors.OrderBy(x => -x.Weight).ToList();
            return result.Where(x => x.Weight > result.ElementAt(0).Weight * 0.1).Select(x => x.Tune()).ToArray();
        }

        [Obsolete]
        public static void CreateLivery(string outputFile, params Color[] color) {
            using (var bitmap = new Bitmap(48, 48))
            using (var graphics = Graphics.FromImage(bitmap)) {
                var max = color.Length > 15 ? 15 : color.Length;
                for (var i = 0; i < max; i++) {
                    graphics.FillRectangle(new SolidBrush(color[i]), 0, (float)Math.Sqrt((double)i / max) * 48, 48, 48);
                }

                bitmap.Save(outputFile, ImageFormat.Png);
            }
        }

        [Obsolete]
        public static void GenerateLivery(string inputFile, string outputFile) {
            using (var image = Image.FromFile(inputFile)){
                CreateLivery(outputFile, GetBaseColors((Bitmap)image));
            }
        }
    }
}
