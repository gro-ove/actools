using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    public class ScreenshotsConverter : IDisposable {
        [CanBeNull]
        public static Task CurrentConversion { get; private set; }

        private readonly string[] _previous;

        private static IEnumerable<string> GetBitmaps() {
            try {
                var directory = FileUtils.GetDocumentsScreensDirectory();
                if (!Directory.Exists(directory)) return new string[0];
                return Directory.GetFiles(directory)
                                .Where(file => file.EndsWith(@".bmp", StringComparison.OrdinalIgnoreCase));
            } catch (Exception e) {
                Logging.Error("[ScreenshotsConverter] Can’t get files without extension: " + e);
                return new string[0];
            }
        }

        private ScreenshotsConverter() {
            _previous = GetBitmaps().ToArray();
        }

        [CanBeNull]
        public static ScreenshotsConverter OnlyNewIfEnabled() {
            return SettingsHolder.Drive.AutomaticallyConvertBmpToJpg ? new ScreenshotsConverter() : null;
        }

        private static void Convert(IEnumerable<string> bitmaps) {
            CurrentConversion = Task.Run(() => {
                try {
                    foreach (var bitmap in bitmaps) {
                        ImageUtils.Convert(bitmap, FileUtils.EnsureUnique(bitmap.ApartFromLast(@".bmp", StringComparison.OrdinalIgnoreCase) + @".jpg"));
                        File.Delete(bitmap);
                    }
                } catch (Exception e) {
                    Logging.Error("[ScreenshotsConverter] Can’t rename new: " + e);
                }

                CurrentConversion = null;
            });
        }

        public void Dispose() {
            Convert(GetBitmaps().Where(x => !_previous.Contains(x)).ToList());
        }
    }
}