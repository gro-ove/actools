using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Processes;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.GameProperties {
    public class AcPatchTrackOutline : Game.RaceIniProperties {
        public override void Set(IniFile file) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                var s = Stopwatch.StartNew();
                try {
                    var trackId = file["RACE"].GetNonEmpty("TRACK");
                    var configurationId = file["RACE"].GetNonEmpty("CONFIG_TRACK");
                    var track = TracksManager.Instance.GetLayoutById(trackId ?? string.Empty, configurationId);
                    if (track == null) return;

                    var outline = track.OutlineImage;
                    var outlineCropped = Path.Combine(Path.GetDirectoryName(track.OutlineImage) ?? "", "outline_cropped.png");
                    if (!File.Exists(outline) || File.Exists(outlineCropped)) return;

                    var image = BetterImage.LoadBitmapSource(outline);
                    var size = new Size(256, 256);

                    var result = new BetterImage {
                        Width = 256,
                        Height = 256,
                        Source = image.ImageSource,
                        CropTransparentAreas = true
                    };

                    result.Measure(size);
                    result.Arrange(new Rect(size));
                    result.ApplyTemplate();
                    result.UpdateLayout();

                    var bmp = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(result);
                    File.WriteAllBytes(outlineCropped, bmp.ToBytes(ImageFormat.Png));
                } finally {
                    Logging.Write($"Time taken: {s.Elapsed.TotalMilliseconds:F2} ms");
                }
            });
        }
    }
}