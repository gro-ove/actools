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
using VerticalAlignment = System.Windows.VerticalAlignment;

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

        /// <summary>
        /// Because I messed up with strings having separate dictionaties per assembly — sometimes, some code has to
        /// be moved to the previous assembly.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        [Localizable(false), NotNull]
        public static string GetString([NotNull] string category, [NotNull] string key) {
            return Registered.GetValueOrDefault(category.ToLowerInvariant())?.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }

        private static FrameworkElement ToImage(byte[] bytes, int? decodeWidth) {
            return ToImage(BetterImage.LoadBitmapSourceFromBytes(bytes, decodeWidth ?? -1).ImageSource);
        }

        private static FrameworkElement ToImage(string filename, int? decodeWidth) {
            return ToImage(BetterImage.LoadBitmapSource(filename, decodeWidth ?? -1).ImageSource);
        }

        private static FrameworkElement ToImage(ImageSource source) {
            var i = new Image { Source = source, Stretch = Stretch.Uniform };
            i.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            i.SetResourceReference(UIElement.EffectProperty, @"TrackOutlineAloneEffect");
            return i;
        }

        private static FrameworkElement ToPath(string geometry) {
            return ToPath(Geometry.Parse(geometry));
        }

        private static FrameworkElement ToPath(Geometry geometry) {
            var p = new Path { Data = geometry, Stretch = Stretch.Uniform };
            p.SetBinding(Shape.FillProperty, new Binding { Path = new PropertyPath(TextBlock.ForegroundProperty), RelativeSource = RelativeSource.Self });
            return p;
        }

        private static FrameworkElement ToBbCodeBlock(string text, int? decodeWidth) {
            var p = new BbCodeBlock {
                Text = text,
                FontSize = (decodeWidth ?? 60d) / 2,
                FontWeight = decodeWidth < 20 ? FontWeights.Bold
                        : decodeWidth < 40 ? FontWeights.Normal : FontWeights.UltraLight,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            p.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);
            return p;
        }

        private static FrameworkElement ToBbCodeBlock(BbCodeBlock original) {
            var p = new BbCodeBlock {
                Text = original.Text,
                FontSize = original.FontSize,
                FontWeight = original.FontWeight,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            p.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);
            return p;
        }

        /// <summary>
        /// Use this method if you want to get an icon from a non-UI thread. You still need to call
        /// the returned value in UI thread to get an icon, but bytesProvider or filenameProvider
        /// will execute from your thread, immediately.
        /// </summary>
        [NotNull]
        public static Func<ElementPool> GetIconInTwoSteps([CanBeNull] string iconValue, Func<string, byte[]> bytesProvider = null,
                Func<string, string> filenameProvider = null, int? decodeWidth = null) {
            if (iconValue == null) return () => ElementPool.EmptyPool;

            try {
                if (iconValue.StartsWith(@"path:", StringComparison.OrdinalIgnoreCase) || iconValue.StartsWith(@"F1 ") && iconValue.EndsWith("Z")) {
                    return () => new IconPool(ToPath(iconValue.ApartFromFirst(@"path:", StringComparison.OrdinalIgnoreCase)));
                }

                if (iconValue.StartsWith(@"text:", StringComparison.OrdinalIgnoreCase)) {
                    return () => new IconPool(ToBbCodeBlock(iconValue.ApartFromFirst(@"text:", StringComparison.OrdinalIgnoreCase), decodeWidth));
                }

                var filename = filenameProvider?.Invoke(iconValue);
                if (filename != null) {
                    if (filename.EndsWith(@".path", StringComparison.OrdinalIgnoreCase)) {
                        var text = File.ReadAllText(filename);
                        return () => new IconPool(ToPath(text));
                    }

                    if (filename.EndsWith(@".txt", StringComparison.OrdinalIgnoreCase)) {
                        var text = File.ReadAllText(filename);
                        return () => new IconPool(ToBbCodeBlock(text, decodeWidth));
                    }

                    return () => new IconPool(ToImage(filename, decodeWidth));
                }

                var data = bytesProvider?.Invoke(iconValue);
                if (data != null) {
                    if (iconValue.EndsWith(@".path", StringComparison.OrdinalIgnoreCase)) {
                        return () => new IconPool(ToPath(data.ToUtf8String()));
                    }

                    if (iconValue.EndsWith(@".txt", StringComparison.OrdinalIgnoreCase)) {
                        return () => new IconPool(ToBbCodeBlock(data.ToUtf8String(), decodeWidth));
                    }

                    return () => new IconPool(ToImage(data, decodeWidth));
                }
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t load icon", e);
            }

            return () => ElementPool.EmptyPool;
        }

        [NotNull]
        public static ElementPool GetIcon([CanBeNull] string iconValue, Func<string, byte[]> bytesProvider = null, Func<string, string> filenameProvider = null,
                int? decodeWidth = null) {
            return GetIconInTwoSteps(iconValue, bytesProvider, filenameProvider, decodeWidth).Invoke();
        }

        [NotNull]
        public static ElementPool GetIcon(string iconValue, string contentCategory, int? decodeWidth = null) {
            return GetIcon(iconValue, filenameProvider: k => FilesStorage.Instance.GetContentFile(contentCategory, k).Filename, decodeWidth: decodeWidth);
        }

        private class IconPool : ElementPool {
            public IconPool([CanBeNull] FrameworkElement original) : base(original) { }

            protected override FrameworkElement CloneContentIcon() {
                switch (Original) {
                    case Image image:
                        return ToImage(image.Source);
                    case Path path:
                        return ToPath(path.Data);
                    case BbCodeBlock bbCodeBlock:
                        return ToBbCodeBlock(bbCodeBlock);
                    default:
                        return null;
                }
            }
        }
    }
}