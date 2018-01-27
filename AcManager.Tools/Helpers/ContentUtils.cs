using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
        public static object GetIcon(string contentCategory, string iconValue) {
            try {
                if (iconValue.StartsWith("path:") || iconValue.StartsWith("F1 ") && iconValue.EndsWith("Z")) {
                    var p = new Path {
                        Data = Geometry.Parse(iconValue.ApartFromFirst("path:")),
                        Stretch = Stretch.Uniform
                    };

                    p.SetBinding(Shape.FillProperty, new Binding {
                        Path = new PropertyPath(TextBlock.ForegroundProperty),
                        RelativeSource = RelativeSource.Self
                    });

                    return p;
                }

                if (iconValue.StartsWith("text:")) {
                    var p = new BbCodeBlock {
                        BbCode = iconValue.ApartFromFirst("text:"),
                        FontSize = 30,
                        FontWeight = FontWeights.UltraLight,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    };

                    p.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);
                    return p;
                }

                var iconFile = FilesStorage.Instance.GetContentFile(contentCategory, iconValue);
                if (iconFile.Exists) {
                    var i = new BetterImage {
                        Filename = iconFile.Filename,
                        ClearOnChange = true
                    };

                    i.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    i.SetResourceReference(UIElement.EffectProperty, "TrackOutlineAloneEffect");
                    return i;
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t load icon", e);
            }

            return null;
        }
    }
}