using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Effects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using MoonSharp.Interpreter;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private class CmTexturesScriptTexture {
            [CanBeNull]
            private readonly ExtraDataProvider _data;

            [NotNull]
            private readonly RaceTexturesContext _texturesContext;

            [NotNull]
            private readonly Action<string, byte[]> _saveCallback;

            private Panel _canvas;
            private readonly double _scale;

            public CmTexturesScriptTexture(Table v, [CanBeNull] ExtraDataProvider data, [NotNull] RaceTexturesContext texturesContext,
                    [NotNull] Action<string, byte[]> saveCallback) {
                _data = data;
                _texturesContext = texturesContext;
                _saveCallback = saveCallback;
                _scale = v[@"scale"].As(1d);

                ActionExtension.InvokeInMainThread(() => {
                    _canvas = new Cell {
                        Width = v[@"width"].As(256d),
                        Height = v[@"height"].As(256d),
                        Background = new SolidColorBrush(GetColor(v, "background", Colors.Transparent))
                    };
                });
            }

            [UsedImplicitly]
            public bool Text(Table v) {
                var text = GetText(v);
                if (text == null) return false;

                var color = GetColor(v);
                var font = v[@"font"]?.ToString();
                var fontSize = v[@"fontSize"].As(30d);

                if (!string.IsNullOrWhiteSpace(font)) {
                    var fontPng = _data?.Get(font + ".png");
                    var fontTxt = _data?.Get(font + ".txt");
                    if (fontPng != null && fontTxt != null) {
                        ActionExtension.InvokeInMainThread(() => {
                            var bitmap = new FontObjectBitmap(fontPng, fontTxt);
                            var charsPanel = new SpacingStackPanel {
                                Spacing = v[@"spacing"].As(0d),
                                Orientation = Orientation.Horizontal
                            };

                            for (var i = 0; i < text.Length; i++) {
                                var charBitmap = bitmap.BitmapForChar(text[i]);
                                if (charBitmap == null) return;
                                charsPanel.Children.Add(new Image {
                                    Source = charBitmap,
                                    Height = fontSize,
                                    Width = fontSize * charBitmap.Width / charBitmap.Height
                                });
                            }

                            ApplyParams(v, charsPanel);
                            charsPanel.Effect = new AsTransparencyMask { OverlayColor = color };
                            _canvas.Children.Add(charsPanel);
                        });
                        return true;
                    }
                }

                ActionExtension.InvokeInMainThread(() => {
                    var block = v[@"bb"].As(false) ? new BbCodeBlock() : new TextBlock();
                    block.Text = text;
                    block.Foreground = new SolidColorBrush(color);
                    block.FontFamily = new FontFamily(font ?? @"Segoe UI");
                    block.FontWeight = FontWeight.FromOpenTypeWeight(v[@"fontWeight"].As(400));
                    block.FontStyle = v[@"fontItalic"].As(false) ? FontStyles.Italic : FontStyles.Normal;
                    block.FontSize = fontSize;
                    ApplyParams(v, block);
                    _canvas.Children.Add(block);
                });
                return true;
            }

            [UsedImplicitly]
            public bool Image(Table v) {
                var data = _data?.Get(GetText(v, "name") ?? "");
                if (data == null) {
                    Logging.Warning($"Image not found: {GetText(v, "name")}");
                    return false;
                }

                ActionExtension.InvokeInMainThread(() => {
                    var image = new Image {
                        Source = BetterImage.LoadBitmapSourceFromBytes(data).ImageSource,
                        Stretch = v[@"stretch"].As(Stretch.Uniform),
                        Effect = GetEffect(v[@"effect"]?.ToString())
                    };
                    ApplyParams(v, image);
                    _canvas.Children.Add(image);
                });
                return true;
            }

            [UsedImplicitly]
            public bool TrackMap(Table v) {
                var filename = _texturesContext.Track?.OutlineImage;
                if (filename == null || !File.Exists(filename)) {
                    Logging.Warning("Outline image not available");
                    return false;
                }

                ActionExtension.InvokeInMainThread(() => {
                    var image = new BetterImage {
                        Source = BetterImage.LoadBitmapSourceFromBytes(File.ReadAllBytes(filename)).ImageSource,
                        CropTransparentAreas = v[@"cropTransparent"].As(true),
                        Stretch = v[@"stretch"].As(Stretch.Uniform),
                        Effect = GetEffect(v[@"effect"]?.ToString())
                    };
                    ApplyParams(v, image);
                    _canvas.Children.Add(image);
                });
                return true;
            }

            [UsedImplicitly]
            public bool WeatherIcon(Table v) {
                var icons = _data?.GetKeys(GetText(v, "icons") ?? "").ToList();
                if (icons == null || icons.Count == 0) {
                    Logging.Warning($"Icons not found: {GetText(v, "icons")}");
                    return false;
                }

                if (_texturesContext.Weather == null) {
                    return false;
                }

                var weatherType = _texturesContext.Weather.Type;
                if (weatherType == WeatherType.None) {
                    weatherType = WeatherObject.TryToDetectWeatherTypeById(_texturesContext.Weather.Id);
                }

                var availableTypes = icons.Select(x => new {
                    Type = Enum.TryParse<WeatherType>(Regex.Replace(x, @"^.*/|\.\w+$|\W", ""), true, out var type) ? type : WeatherType.None,
                    FileName = x
                }).ToList();

                var closestType = weatherType.FindClosestWeather(availableTypes.Select(x => x.Type));
                if (closestType == null || closestType == WeatherType.None) {
                    Logging.Warning($"Closest icon not found: {GetText(v, "icons")}");
                    return false;
                }

                var data = _data?.Get(availableTypes.FirstOrDefault(x => x.Type == closestType)?.FileName ?? string.Empty);
                if (data == null) {
                    Logging.Warning($"Icon not found: closestType={closestType}, fileName={availableTypes.FirstOrDefault(x => x.Type == closestType)?.FileName}");
                    return false;
                }

                ActionExtension.InvokeInMainThread(() => {
                    var image = new Image {
                        Source = BetterImage.LoadBitmapSourceFromBytes(data).ImageSource,
                        Stretch = v[@"stretch"].As(Stretch.Uniform),
                        Effect = GetEffect(v[@"effect"]?.ToString())
                    };
                    ApplyParams(v, image);
                    _canvas.Children.Add(image);
                });
                return true;
            }

            [UsedImplicitly]
            public void Save(string name) {
                ActionExtension.InvokeInMainThread(() => {
                    var size = new Size(_canvas.Width, _canvas.Height);
                    _canvas.Measure(size);
                    _canvas.Arrange(new Rect(size));
                    _canvas.ApplyTemplate();
                    _canvas.UpdateLayout();

                    foreach (var block in _canvas.FindVisualChildren<BbCodeBlock>()) {
                        block.ForceUpdate();
                    }

                    var dpi = (96 * _scale).RoundToInt();
                    var bmp = new RenderTargetBitmap((_canvas.Width * _scale).RoundToInt(), (_canvas.Height * _scale).RoundToInt(),
                            dpi, dpi, PixelFormats.Pbgra32);
                    bmp.Render(_canvas);
                    _saveCallback.Invoke(name, bmp.ToBytes(ImageFormat.Png));
                });
            }

            private static Color GetColor(Table v, [Localizable(false)] string key = "color", Color? fallback = null) {
                return v?[key]?.ToString().ToColor() ?? fallback ?? Colors.White;
            }

            [CanBeNull]
            private string GetText(Table v, [Localizable(false)] string key = "text") {
                var s = v?[key]?.ToString();
                return s == null ? null : Regex.Replace(s, @"\{([A-Z]\w+)\}", m => {
                    switch (m.Groups[1].Value) {
                        case "Temperature":
                            return _texturesContext.Temperature?.ToInvariantString();
                        case "Wind":
                            return _texturesContext.Wind?.ToInvariantString();
                        case "WindDirection":
                            return _texturesContext.WindDirection?.ToInvariantString();
                        case "WindDirectionShort":
                            return _texturesContext.WindDirection?.ToDisplayWindDirection();
                        case "TrackName":
                            return _texturesContext.Track?.MainTrackObject.DisplayNameWithoutCount;
                        default:
                            return m.Value;
                    }
                });
            }

            private void ApplyParams(Table v, FrameworkElement fe) {
                var top = v[@"y"].As(0d);
                var left = v[@"x"].As(0d);
                var height = v[@"height"].As(0d);
                var width = v[@"width"].As(0d);

                var rotate = v[@"rotate"].As(0d);
                var scaleX = v[@"scaleX"].As(1d);
                var scaleY = v[@"scaleY"].As(1d);

                if (scaleX != 1d || scaleY != 1d) {
                    if (rotate != 0d) {
                        fe.LayoutTransform = new TransformGroup {
                            Children = {
                                new ScaleTransform { ScaleX = scaleX, ScaleY = scaleY },
                                new RotateTransform { Angle = rotate },
                            }
                        };
                    } else {
                        fe.LayoutTransform = new ScaleTransform { ScaleX = scaleX, ScaleY = scaleY };
                    }
                } else if (rotate != 0d) {
                    fe.LayoutTransform = new RotateTransform { Angle = rotate };
                }

                fe.HorizontalAlignment = v[@"horizontalAlignment"].As(HorizontalAlignment.Left);
                fe.VerticalAlignment = v[@"verticalAlignment"].As(VerticalAlignment.Top);
                fe.Margin = new Thickness(
                        fe.HorizontalAlignment == HorizontalAlignment.Right ? (width <= 0d ? 0 : _canvas.Width - width + left) : left,
                        fe.VerticalAlignment == VerticalAlignment.Bottom ? (height <= 0d ? 0 : _canvas.Height - height + top) : top,
                        fe.HorizontalAlignment == HorizontalAlignment.Right ? -left : width <= 0d ? 0 : _canvas.Width - width - left,
                        fe.VerticalAlignment == VerticalAlignment.Bottom ? -top : height <= 0d ? 0 : _canvas.Height - height - top);
            }
        }

        private static bool _scriptRegistered;

        private void RunCmTexturesScript(string script, [CanBeNull] ExtraDataProvider data, [NotNull] RaceTexturesContext texturesContext,
                [NotNull] Action<string, byte[]> saveCallback) {
            if (!_scriptRegistered) {
                _scriptRegistered = true;
                UserData.RegisterType<CmTexturesScriptTexture>();
                UserData.RegisterType<RaceTexturesContext>();
            }

            var state = LuaHelper.GetExtended();
            if (state == null) {
                NonfatalError.NotifyBackground("Can’t run car textures script", "Lua interpreter failed to initialize.");
                return;
            }

            state.Globals[@"Stretch"] = LuaHelper.ToMoonSharp<Stretch>();
            state.Globals[@"HorizontalAlignment"] = LuaHelper.ToMoonSharp<HorizontalAlignment>();
            state.Globals[@"VerticalAlignment"] = LuaHelper.ToMoonSharp<VerticalAlignment>();
            state.Globals[@"OverlayColor"] = new Func<string, string>(s => @"Color:" + s);
            state.Globals[@"Grayscale"] = new Func<string>(() => @"Grayscale");
            state.Globals[@"Invert"] = new Func<string>(() => @"Invert");
            state.Globals[@"InvertKeepColors"] = new Func<string>(() => @"InvertKeepColors");

            state.Globals[@"Texture"] = new Func<Table, CmTexturesScriptTexture>(v =>
                    new CmTexturesScriptTexture(v ?? new Table(state), data, texturesContext, saveCallback));
            state.Globals[@"NoTexture"] = new Action<string>(v => saveCallback(v, null));

            state.Globals[@"input"] = texturesContext;
            state.DoString(script);
        }
    }
}