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

            public Color Color {
                get {
                    var hi = Convert.ToInt32(Math.Floor(H / 60)) % 6;
                    var f = H / 60 - Math.Floor(H / 60);

                    V = V * 255;
                    var v = Convert.ToInt32(V);
                    var p = Convert.ToInt32(V * (1 - S));
                    var q = Convert.ToInt32(V * (1 - f * S));
                    var t = Convert.ToInt32(V * (1 - (1 - f) * S));

                    if (hi == 0)
                        return Color.FromArgb(255, v, t, p);
                    if (hi == 1)
                        return Color.FromArgb(255, q, v, p);
                    if (hi == 2)
                        return Color.FromArgb(255, p, v, t);
                    if (hi == 3)
                        return Color.FromArgb(255, p, q, v);
                    if (hi == 4)
                        return Color.FromArgb(255, t, p, v);
                    return Color.FromArgb(255, v, p, q);
                }
            }
        }

        public static void ColorToHsv(Color color, out double hue, out double saturation, out double value) {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0 : 1d - (1d * min / max);
            value = max / 255d;
        }

        public static Color[] GetBaseColors(Bitmap bitmap, bool forceNonAlphaMode = false) {
            var alphaMode = !forceNonAlphaMode && bitmap.PixelFormat == PixelFormat.Format32bppArgb || bitmap.PixelFormat == PixelFormat.Format32bppPArgb;

            var colors = new List<ColorEntry>(50);
            var skip = alphaMode ? 0 : 8;
            for (var y = skip; y < 48 - skip; y++) {
                for (var x = skip; x < 48 - skip; x++) {
                    var color = bitmap.GetPixel(x, y);
                    double h, s, v;
                    ColorToHsv(color, out h, out s, out v);

                    var w = 1.0 - Math.Abs(y - 16) / 36.0 - Math.Abs(y - 16) / 36.0;
                    if (alphaMode){
                        if (color.A < 240) {
                            continue;
                        }

                        w *= 0.4 + Math.Pow(Math.Min(v, 0.5) / 0.5, 3.0) + s;
                    } else {
                        w *= 0.001 + Math.Pow(Math.Min(v, 0.8) / 0.8, 1.0) + s;
                    }

                    var t = alphaMode ? 0.15 : 0.25;

                    for (var i = 0; i < colors.Count; i++) {
                        var en = colors[i];
                        if (!(en.Distance(h, s, v) < t)) continue;

                        en.Add(h, s, v, w);

                        for (var j = i + 1; j < colors.Count; j++) {
                            var em = colors[j];
                            if (!(em.Distance(en.H, en.S, en.V) < t)) continue;

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

                    loop: ;
                }
            }

            var result = colors.OrderBy(x => -x.Weight);
            return result.Take(alphaMode ? 2 : 1).Where(x => x.Weight > result.ElementAt(0).Weight * 0.1).Select(x => x.Color).ToArray();
        }

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

        public static void GenerateLivery(string inputFile, string outputFile) {
            using (var image = Image.FromFile(inputFile))
            using (var bitmap = new Bitmap(image, new Size(48, 48))) {
                CreateLivery(outputFile, GetBaseColors(bitmap, true));
            }
        }
    }
}
