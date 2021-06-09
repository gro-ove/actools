using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ImageMagick;
using JetBrains.Annotations;
using MoonSharp.Interpreter;

namespace LicensePlates {
    public class LicensePlatesStyle : IDisposable {
        private readonly string _directory;

        private readonly Script _state;

        private readonly PlateParams _plateParams;

        [NotNull]
        private readonly TextParams _textParams;

        private readonly List<PlateValueBase> _inputParams;
        private readonly Closure _closure;

        [CanBeNull]
        private MagickImage _textLayer;

        [CanBeNull]
        private MagickImage _textFlatLayer;

        private readonly Dictionary<string, MagickImage> _images = new Dictionary<string, MagickImage>();

        private MagickImage LoadImage(string filename) {
            filename = filename.ToLowerInvariant();
            if (_images.TryGetValue(filename, out var result)) return result;

            try {
                return _images[filename] = new MagickImage(filename);
            } catch (Exception) {
                return _images[filename] = new MagickImage(MagickColors.Transparent, 4, 4);
            }
        }

        public IReadOnlyList<PlateValueBase> InputParams => _inputParams;

        private static Script CreateState() {
            var state = new Script();

            state.Globals[@"Gravity"] = ToMoonSharp<Gravity>();
            state.Globals[@"FontWeight"] = ToMoonSharp<FontWeight>();
            state.Globals[@"InputLength"] = ToMoonSharp<InputLength>();

            state.Globals[@"generateRandomString"] = new Func<string, int, string>((allowed, length) => {
                var result = new StringBuilder(length);
                for (var i = 0; i < length; i++) {
                    result.Append(allowed[PlateValueBase.Random.Next(0, allowed.Length)]);
                }

                return result.ToString();
            });

            UserData.RegisterType<TextSize>();
            UserData.RegisterType<PlateParams>();
            UserData.RegisterType<TextParams>();
            UserData.RegisterType<MoonSharpFsHelper>();
            UserData.RegisterType<MoonSharpPathHelper>();

            return state;
        }

        public class TextSize {
            public double X, Y;

            public TextSize(double x, double y) {
                X = x;
                Y = y;
            }
        }

        // ReSharper disable UnusedMember.Local
        private class MoonSharpFsHelper {
            public string[] ReadDir(string directory, string filter = "*") => Directory.GetFiles(directory, filter);
            public string ReadFile(string filename) => File.ReadAllText(filename);
            public bool Exists(string filename) => File.Exists(filename) || Directory.Exists(filename);
            public bool FileExists(string filename) => File.Exists(filename);
            public bool DirExists(string filename) => Directory.Exists(filename);
        }

        private class MoonSharpPathHelper {
            public string Combine(params string[] args) => Path.Combine(args);
            public string GetFileName(string filename) => Path.GetFileName(filename);
            public string GetFileNameWithoutExtension(string filename) => Path.GetFileNameWithoutExtension(filename);
        }

        // ReSharper restore UnusedMember.Local

        private static object DefaultValue(DynValue value) {
            if (value == null) return null;

            if (value.Function != null) {
                return new Func<string>(() => value.Function.Call().CastToString());
            }

            return value.CastToString();
        }

        private void BindState() {
            _state.Globals["showMsg"] = new Action<string>(m => { MessageBox.Show(m); });

            _state.Globals["fs"] = new MoonSharpFsHelper();
            _state.Globals["path"] = new MoonSharpPathHelper();

            _state.Globals["location"] = _directory;
            _state.Globals["style"] = Path.GetDirectoryName(_directory);
            _state.Globals["plate"] = UserData.Create(_plateParams);
            _state.Globals["text"] = UserData.Create(_textParams);

            _state.Globals["defineSelect"] = new Action<string, Dictionary<string, string>, DynValue>((name, values, defaultValue) => {
                _inputParams.Add(values.Keys.Where((s, i) => s != (i + 1).ToString(CultureInfo.InvariantCulture)).Any()
                        ? new InputSelectValue(name, DefaultValue(defaultValue), values.ToList())
                        : new InputSelectValue(name, DefaultValue(defaultValue), values.Values.Select(x => new KeyValuePair<string, string>(x, x)).ToList()));
            });

            _state.Globals["defineText"] =
                    new Action<string, int, int, DynValue>(
                            (name, length, lengthMode, defaultValue) => {
                                _inputParams.Add(new InputTextValue(name, DefaultValue(defaultValue), length, (InputLength)lengthMode));
                            });

            _state.Globals["defineNumber"] =
                    new Action<string, int, int, int, DynValue>(
                            (name, length, from, to, defaultValue) => {
                                _inputParams.Add(new InputNumberValue(name, DefaultValue(defaultValue), length, from, to));
                            });
        }

        private void RebindState() {
            _state.Globals.Remove("defineSelect");
            _state.Globals.Remove("defineText");
            _state.Globals.Remove("defineNumber");

            _state.Globals["measureText"] = new Func<string, double, double, int?, TextSize>(GetTextSize);
            _state.Globals["drawText"] = new Func<string, double, double, int?, TextSize>(DrawText);
            _state.Globals["drawFlatText"] = new Func<string, double, double, int?, TextSize>(DrawFlatText);
        }

        private TextSize TextFn([NotNull] MagickImage layer, string v, double x, double y, int? position, bool measureOnly) {
            var current = Environment.CurrentDirectory;
            try {
                Environment.CurrentDirectory = _directory;

                layer.Settings.FillColor = new MagickColor(_textParams.Color);
                layer.Settings.Font = _textParams.Font;
                layer.Settings.FontPointsize = _textParams.Size * _plateParams.SizeMultipler;
                layer.Settings.FontWeight = (FontWeight)_textParams.Weight;
                layer.Settings.TextKerning = _textParams.Kerning * _plateParams.SizeMultipler;

                if (_textParams.LineSpacing.HasValue) {
                    layer.Settings.TextInterlineSpacing = _textParams.LineSpacing.Value * _plateParams.SizeMultipler;
                }

                var spaces = _textParams.GetSpaces()?.ToArray() ?? new double[0];
                for (int i = 0; i < spaces.Length; i++) {
                    spaces[i] = spaces[i] * _plateParams.SizeMultipler;
                }

                return DrawText(layer, v, spaces, x * _plateParams.SizeMultipler, y * _plateParams.SizeMultipler,
                        position.HasValue ? (Gravity)position : Gravity.Northwest, measureOnly);
            } finally {
                Environment.CurrentDirectory = current;
            }
        }

        private TextSize GetTextSize(string v, double x, double y, int? position) {
            return TextFn(EnsureTextLayerIsCreated(), v, x, y, position, true);
        }

        private TextSize DrawText(string v, double x, double y, int? position) {
            return TextFn(EnsureTextLayerIsCreated(), v, x, y, position, false);
        }

        private TextSize DrawFlatText(string v, double x, double y, int? position) {
            return TextFn(EnsureTextFlatLayerIsCreated(), v, x, y, position, false);
        }

        public LicensePlatesStyle(string directory) {
            _directory = directory;

            _plateParams = new PlateParams();
            _textParams = new TextParams();
            _inputParams = new List<PlateValueBase>();
            _state = CreateState();

            var current = Environment.CurrentDirectory;
            Environment.CurrentDirectory = _directory;

            try {
                BindState();

                _closure = _state.DoFile(Path.Combine(directory, "style.lua")).Function;
                if (_closure == null) {
                    throw new Exception("Style’s script should return function");
                }

                RebindState();
            } finally {
                Environment.CurrentDirectory = current;
            }
        }

        private string _valuesFootprint;

        private void EnsureTextLayerIsActual() {
            var values = _inputParams.Select(x => (object)x.ResultValue).ToArray();
            var valuesFootprint = string.Join("\n", _inputParams.Select(x => (object)x.Value));
            if (valuesFootprint != _valuesFootprint) {
                if (_textLayer != null) {
                    _textLayer.Evaluate(Channels.All, EvaluateOperator.Set, 0d);
                }

                var current = Environment.CurrentDirectory;
                Environment.CurrentDirectory = _directory;

                try {
                    _closure.Call(values);
                } finally {
                    Environment.CurrentDirectory = current;
                }

                _valuesFootprint = valuesFootprint;
            }
        }

        [NotNull]
        private MagickImage EnsureTextLayerIsCreated() {
            if (_textLayer == null) {
                if (_plateParams.Size == null) {
                    var background = LoadImage(Path.Combine(_directory, _plateParams.Background));
                    _plateParams.Size = new[] { background.Width, background.Height };
                }

                _textLayer = new MagickImage(MagickColors.Transparent,
                        (int)(_plateParams.Size[0] * _plateParams.SizeMultipler),
                        (int)(_plateParams.Size[1] * _plateParams.SizeMultipler));
            }
            return _textLayer;
        }

        [NotNull]
        private MagickImage EnsureTextFlatLayerIsCreated() {
            if (_textFlatLayer == null) {
                var textLayer = EnsureTextLayerIsCreated();
                _textFlatLayer = new MagickImage(MagickColors.Transparent, textLayer.Width, textLayer.Height);
                _textFlatLayer.Evaluate(Channels.All, EvaluateOperator.Set, 0d);
            }
            return _textFlatLayer;
        }

        public MagickImage CreateDiffuseMap(bool previewMode) {
            EnsureTextLayerIsActual();
            return DiffuseMap(_textLayer, _textFlatLayer, Path.Combine(_directory, _plateParams.Background), _plateParams.Light, previewMode);
        }

        public MagickImage CreateNormalsMap(bool previewMode) {
            EnsureTextLayerIsActual();
            return NormalsMap(_textLayer, Path.Combine(_directory, _plateParams.Normals), previewMode);
        }

        public void CreateDiffuseMap(bool previewMode, string filename) {
            using (var image = CreateDiffuseMap(previewMode)) {
                image.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt5");
                image.Settings.SetDefine(MagickFormat.Dds, "mipmaps", "false");
                image.Settings.SetDefine(MagickFormat.Dds, "cluster-fit", "true");
                image.Write(filename);
            }
        }

        public void CreateNormalsMap(bool previewMode, string filename) {
            using (var image = CreateNormalsMap(previewMode)) {
                image.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt5");
                image.Settings.SetDefine(MagickFormat.Dds, "mipmaps", "false");
                image.Settings.SetDefine(MagickFormat.Dds, "cluster-fit", "true");
                image.Write(filename);
            }
        }

        public enum Format {
            Png = MagickFormat.Png,
            Dds = MagickFormat.Dds,
            Jpeg = MagickFormat.Jpeg,
        }

        public byte[] CreateDiffuseMap(bool previewMode, Format format) {
            using (var image = CreateDiffuseMap(previewMode)) {
                image.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt5");
                return image.ToByteArray((MagickFormat)format);
            }
        }

        public byte[] CreateNormalsMap(bool previewMode, Format format) {
            using (var image = CreateNormalsMap(previewMode)) {
                image.Settings.SetDefine(MagickFormat.Dds, "compression", "dxt5");
                return image.ToByteArray((MagickFormat)format);
            }
        }

        private static TypeMetric Measure(MagickImage image, string line, bool ignoreNewLines = false) {
            return string.IsNullOrEmpty(line) ? image.FontTypeMetrics("​") : image.FontTypeMetrics(line, ignoreNewLines);
        }

        private static TextSize DrawText(MagickImage image, string line, double[] spaces, double offsetX, double offsetY, Gravity position, bool measureOnly) {
            if (string.IsNullOrWhiteSpace(line)) return new TextSize(0d, 0d);

            image.Settings.TextAntiAlias = true;
            // image.Settings.TextInterwordSpacing = 0;
            // image.Settings.TextInterlineSpacing = 0;

            if (spaces == null || spaces.Length == 0) {
                if (!measureOnly) {
                    var gravity = new DrawableGravity(position);
                    var drawable = new DrawableText(offsetX, offsetY, line);
                    image.Draw(drawable, gravity);
                }

                var metrics = Measure(image, line);
                return new TextSize(metrics.TextWidth, metrics.Ascent - metrics.Descent);
            } else {
                var words = line.Split(' ');
                var metrics = words.Select(x => Measure(image, x, true)).ToArray();

                var totalSpace = words.Take(words.Length - 1).Select((x, i) => i < spaces.Length ? spaces[i] : spaces.LastOrDefault()).Sum();
                var totalWidth = metrics.Select(x => x.TextWidth).Sum() + totalSpace;
                var totalHeight = metrics.Select(x => x.Ascent - x.Descent).Max();

                if (!measureOnly) {
                    double currentX, currentY;

                    switch (position) {
                        case Gravity.West:
                        case Gravity.Northwest:
                        case Gravity.Southwest:
                        case Gravity.Undefined:
                            currentX = offsetX;
                            break;
                        case Gravity.North:
                        case Gravity.Center:
                        case Gravity.South:
                            currentX = image.Width / 2d - totalWidth / 2 + offsetX;
                            break;
                        case Gravity.East:
                        case Gravity.Northeast:
                        case Gravity.Southeast:
                            currentX = image.Width - totalWidth - offsetX;
                            break;
                        default:
                            currentX = 0d;
                            break;
                    }

                    switch (position) {
                        case Gravity.Northwest:
                        case Gravity.North:
                        case Gravity.Northeast:
                        case Gravity.Undefined:
                            currentY = offsetY;
                            break;
                        case Gravity.West:
                        case Gravity.Center:
                        case Gravity.East:
                            currentY = image.Height / 2d - totalHeight / 2 + offsetY;
                            break;
                        case Gravity.Southwest:
                        case Gravity.South:
                        case Gravity.Southeast:
                            currentY = image.Height - totalHeight - offsetY;
                            break;
                        default:
                            currentY = 0d;
                            break;
                    }

                    var gravity = new DrawableGravity(Gravity.Northwest);
                    for (var i = 0; i < words.Length; i++) {
                        if (!string.IsNullOrWhiteSpace(words[i])) {
                            image.Draw(new DrawableText(currentX, currentY, words[i]), gravity);
                        }

                        currentX += metrics[i].TextWidth + (i < spaces.Length ? spaces[i] : spaces.LastOrDefault());
                    }
                }

                return new TextSize(totalWidth, totalHeight);
            }
        }

        private static void DrawBevel(MagickImage image, MagickImage textLayer, double lightDirection) {
            using (var bevelLayer = new MagickImage(MagickColors.White, textLayer.Width, textLayer.Height)) {
                bevelLayer.Composite(textLayer, 0, 0, CompositeOperator.Over);
                bevelLayer.Shade(lightDirection, 30);
                bevelLayer.Blur(10, 6);
                image.Composite(bevelLayer, 0, 0, CompositeOperator.LinearLight);
            }
        }

        private MagickImage DiffuseMap(MagickImage textLayer, MagickImage textFlatLayer, string backgroundFile, double lightDirection, bool previewMode) {
            var image = LoadImage(backgroundFile).Clone();
            if (_disposed) return image;

            if (image.Width != textLayer.Width || image.Height != textLayer.Height) {
                if (_disposed) return image;
                image.Resize(textLayer.Width, textLayer.Height);
            }

            if (!previewMode) {
                if (_disposed) return image;
                DrawBevel(image, textLayer, lightDirection);
            }

            if (_disposed) return image;
            image.Composite(textLayer, 0, 0, CompositeOperator.Over);

            if (_disposed) return image;
            if (textFlatLayer != null) {
                image.Composite(textFlatLayer, 0, 0, CompositeOperator.Over);
            }

            var textAlpha = textLayer.Clone();
            // textAlpha.HasAlpha = false;
            // textAlpha.FloodFill(); = false;
            // textAlpha.Negate();
            // textAlpha.Level(255, 240);
            image.Evaluate(Channels.Alpha, EvaluateOperator.Set, new Percentage(0d));
            textAlpha.InverseLevel(255, 215, Channels.Alpha);
            image.Composite(textAlpha, 0, 0, CompositeOperator.CopyAlpha);
            return image;
        }

        private static MagickImage GetNormalsMapLayer(MagickImage textLayer) {
            var layer = new MagickImage(MagickColors.White, textLayer.Width, textLayer.Height);
            using (var blackText = new MagickImage(MagickColors.Black, textLayer.Width, textLayer.Height)) {
                blackText.Composite(textLayer, 0, 0, CompositeOperator.CopyAlpha);
                layer.Composite(blackText, 0, 0, CompositeOperator.Over);
            }

            layer.Resize(new Percentage(200));
            layer.Morphology(MorphologyMethod.Dilate, Kernel.Diamond, 1);
            layer.Resize(textLayer.Width, textLayer.Height);

            layer.Shade(0, 30, true, Channels.Red);
            layer.Shade(-90, 30, true, Channels.Green);
            layer.Evaluate(Channels.Blue, EvaluateOperator.Set, new Percentage(100d));

            return layer;
        }

        private static void NormalizeNormalsMap(MagickImage image, double maxHorizontalLength) {
            var m2 = maxHorizontalLength * maxHorizontalLength;
            using (var pc = image.GetPixels()) {
                foreach (var p in pc) {
                    var x = p.GetChannel(0) / 255.0 * 2.0 - 1.0;
                    var y = p.GetChannel(1) / 255.0 * 2.0 - 1.0;
                    var x2 = x * x;
                    var y2 = y * y;

                    if (x2 + y2 > m2) {
                        var m = maxHorizontalLength / Math.Sqrt(x2 + y2);
                        x *= m;
                        y *= m;
                        x2 *= m;
                        y2 *= m;

                        p.SetChannel(0, (byte)(255 * (x * 0.5 + 0.5)));
                        p.SetChannel(1, (byte)(255 * (y * 0.5 + 0.5)));
                    }

                    if (Math.Abs(x) > 0.1 || Math.Abs(y) > 0.1) {
                        var z = Math.Sqrt(Math.Max(1.0 - x2 - y2, 0d));
                        p.SetChannel(2, (byte)(255 * (z * 0.5 + 0.5)));
                        pc.Set(p);
                    }
                }
            }
        }

        private MagickImage NormalsMap(MagickImage textLayer, string backgroundFile, bool previewMode) {
            var image = LoadImage(backgroundFile).Clone();
            if (_disposed) return image;

            if (image.Width != textLayer.Width || image.Height != textLayer.Height) {
                image.Resize(textLayer.Width, textLayer.Height);
                if (_disposed) return image;
            }

            using (var summaryLayer = GetNormalsMapLayer(textLayer)) {
                if (_disposed) return image;

                if (!previewMode) {
                    using (var blurredLayer = new MagickImage(summaryLayer)) {
                        blurredLayer.Blur(12, 6);
                        if (_disposed) return image;
                        image.Composite(blurredLayer, 0, 0, CompositeOperator.Overlay);
                        if (_disposed) return image;
                    }

                    using (var textFlatten = new MagickImage(textLayer)) {
                        textFlatten.Colorize(new MagickColor("#8080ff"), new Percentage(100));
                        if (_disposed) return image;
                        image.Composite(textFlatten, 0, 0, CompositeOperator.Over);
                        if (_disposed) return image;
                    }
                }

                image.Composite(summaryLayer, 0, 0, CompositeOperator.Overlay);
                if (_disposed) return image;

                if (!previewMode) {
                    NormalizeNormalsMap(image, 1.0);
                    if (_disposed) return image;
                }
            }

            return image;
        }

        private static object ToMoonSharp<T>() where T : struct {
            return Enum.GetValues(typeof(T)).OfType<T>().Distinct().ToDictionary(x => x.ToString(), x => (int)(object)x);
        }

        public class PlateParams {
            [CanBeNull]
            public int[] Size;

            public double SizeMultipler = 1.0;
            public string Background = "background.png";
            public string Normals = "nm.png";
            public double Kerning = -4d;
            public double Light = -90;
        }

        public class TextParams {
            [CanBeNull]
            public object Spaces = null;

            public string Font = "default.ttf";
            public double Size = 210d;
            public int Weight = (int)FontWeight.Normal;
            public string Color = "#473e29";
            public double Kerning = -4d;
            public double? LineSpacing;

            public double[] GetSpaces() {
                return Spaces == null ? null : Spaces is double ? new[] { (double)Spaces } : (double[])Spaces;
            }
        }

        private bool _disposed;

        public void Dispose() {
            _disposed = true;

            _textLayer?.Dispose();
            _textLayer = null;

            _textFlatLayer?.Dispose();
            _textFlatLayer = null;

            foreach (var image in _images.Values) {
                image.Dispose();
            }

            _images.Clear();
        }
    }
}