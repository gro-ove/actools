using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Path = System.Windows.Shapes.Path;

namespace AcManager.Tools.Helpers {
    public static class ContentUtils {
        private static readonly Dictionary<string, ResourceManager> Registered = new Dictionary<string, ResourceManager>();

        public static void Register([Localizable(false), NotNull] string key, [NotNull] ResourceManager resourceManager) {
            Registered[key.ToLowerInvariant()] = resourceManager;
        }

        private static string GetString(string piece) {
            ResourceManager resourceManager;
            string key;

            var s = piece.Split(new[] { '.' }, 2);
            if (s.Length == 1) {
                resourceManager = Registered.Values.FirstOrDefault();
                key = piece;
            } else {
                resourceManager = Registered.GetValueOrDefault(s[0].ToLowerInvariant());
                key = s[1];
            }

            return resourceManager?.GetString(key, CultureInfo.CurrentUICulture);
        }

        [ContractAnnotation("null => null; notnull => notnull")]
        public static string Translate(string line) {
            return !(line?.Length > 2) || line.IndexOf('{') == -1 ? line
                    : Regex.Replace(line, @"\{(\w+(?:\.\w+)?)\}", m => GetString(m.Groups[1].Value) ?? m.Value);
        }

        [CanBeNull]
        public static object GetIcon([NotNull] string iconValue, Func<string, byte[]> bytesProvider = null, Func<string, string> filenameProvider = null) {
            try {
                if (iconValue.StartsWith("path:", StringComparison.OrdinalIgnoreCase) || iconValue.StartsWith("F1 ") && iconValue.EndsWith("Z")) {
                    return Vector(iconValue.ApartFromFirst("path:", StringComparison.OrdinalIgnoreCase));
                }

                if (iconValue.StartsWith("text:", StringComparison.OrdinalIgnoreCase)) {
                    return Text(iconValue.ApartFromFirst("text:", StringComparison.OrdinalIgnoreCase));
                }

                var filename = filenameProvider?.Invoke(iconValue);
                if (filename != null) {
                    if (filename.EndsWith(".path", StringComparison.OrdinalIgnoreCase)) {
                        return Vector(File.ReadAllText(filename));
                    }

                    if (filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) {
                        return Text(File.ReadAllText(filename));
                    }

                    return RasterFromFile(filename);
                }

                var data = bytesProvider?.Invoke(iconValue);
                if (data != null) {
                    if (iconValue.EndsWith(".path", StringComparison.OrdinalIgnoreCase)) {
                        return Vector(data.ToUtf8String());
                    }

                    if (iconValue.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) {
                        return Text(data.ToUtf8String());
                    }

                    return Raster(data);
                }
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t load icon", e);
            }

            return null;

            object Raster(byte[] data) {
                var i = new BetterImage { Source = data, ClearOnChange = true };
                i.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                i.SetResourceReference(UIElement.EffectProperty, "TrackOutlineAloneEffect");
                return i;
            }

            object RasterFromFile(string filename) {
                var i = new BetterImage { Filename = filename, ClearOnChange = true };
                i.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                i.SetResourceReference(UIElement.EffectProperty, "TrackOutlineAloneEffect");
                return i;
            }

            object Vector(string data) {
                var p = new Path { Data = Geometry.Parse(data), Stretch = Stretch.Uniform };
                p.SetBinding(Shape.FillProperty, new Binding { Path = new PropertyPath(TextBlock.ForegroundProperty), RelativeSource = RelativeSource.Self });
                return p;
            }

            object Text(string data) {
                var p = new BbCodeBlock {
                    BbCode = data,
                    FontSize = 30,
                    FontWeight = FontWeights.UltraLight,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                p.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);
                return p;
            }
        }

        [CanBeNull]
        public static object GetIcon(string iconValue, string contentCategory) {
            return GetIcon(iconValue, filenameProvider: k => FilesStorage.Instance.GetContentFile(contentCategory, k).Filename);
        }
    }
}