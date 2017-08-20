using System.Drawing;
using System.IO;

namespace AcTools.Render.Utils {
    public enum DdsFormat {
        Auto, Dxt1, Dxt5, GrayscaleDictionary, NoCompression
    }

    public static class DdsEncoder {
        public static void SaveAsDds(string filename, byte[] imageData, DdsFormat format) {
            using (var input = new MemoryStream(imageData))
            using (var image = Image.FromStream(input)) {
                new Compressor {
                    MipmapGeneration = true,
                    MipmapMaxLevel = -1,
                    RoundMode = CompressRoundMode.ToPreviousPowerOfTwo,
                    Format = CompressFormat.DXT1,
                    Quality = CompressQuality.Highest,
                    ResizeFilter = CompressResizeFilter.Mitchell
                }.Process(image.Width, image.Height, b, result,
                        new CompressProgress(v => $"{v*100:F1}%".Dump()),
                        new CompressLog(s => s.Dump()));
            }
        }
    }
}