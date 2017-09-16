using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SlimDX.DirectWrite;
using Color = System.Windows.Media.Color;
using FontStyle = SlimDX.DirectWrite.FontStyle;

namespace AcManager.PaintShop {
    public static partial class PaintShop {
        public static string NameToId(string name, bool upperFirst) {
            var s = Regex.Split(name.ToLowerInvariant(), @"\W+");
            var b = new StringBuilder();

            if (!upperFirst) {
                b.Append(s[0]);
            }

            for (var i = upperFirst ? 0 : 1; i < s.Length; i++) {
                var p = s[i];
                if (p.Length < 1) continue;

                b.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) {
                    b.Append(p.Substring(1));
                }
            }

            return b.ToString();
        }

        [CanBeNull, ContractAnnotation(@"defaultValue: notnull => notnull")]
        private static string GetString(JObject j, string key, string defaultValue = null) {
            return j.GetStringValueOnly(key) ?? defaultValue;
        }

        private class MissingValueException : Exception {
            public string Key { get; }

            public MissingValueException(string key, string message = null) : base(message ?? $"Value required: “{key}”") {
                Key = key;
            }
        }

        [NotNull]
        private static string RequireString(JObject j, string key) {
            var s = j.GetStringValueOnly(key);
            if (s == null) {
                throw new MissingValueException(key);
            }
            return s;
        }

        private static Color GetColor([CanBeNull] JToken j, Color? defaultColor = null) {
            return (j?.ToString().ToColor() ?? defaultColor) ?? Colors.White;
        }

        private static Color GetColor([CanBeNull] JObject j, string key, Color? defaultColor = null) {
            return GetColor(j?[key], defaultColor);
        }

        private static double GetDouble(JObject j, string key, double defaultValue = 0d) {
            var s = j.GetStringValueOnly(key);
            return s == null ? defaultValue : FlexibleParser.TryParseDouble(s) ?? defaultValue;
        }

        private static PaintShopSourceParams GetSourceParams(JObject jObj) {
            return new PaintShopSourceParams {
                NormalizeMax = jObj.GetBoolValueOnly(KeyAutoLevel) ?? false,
                Desaturate = jObj.GetBoolValueOnly(KeyDesaturate) ?? false
            };
        }

        private static PaintShopSourceParams GetMapsSourceParams(JObject jObj) {
            return new PaintShopSourceParams {
                NormalizeMax = jObj.GetBoolValueOnly(KeyMapsAutoLevel) ?? false,
                Desaturate = jObj.GetBoolValueOnly(KeyMapsDesaturate) ?? false
            };
        }

        private static Size? GetSize(JToken token) {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.None) return null;

            if (token.Type == JTokenType.Object) {
                var x = token.GetIntValueOnly("width", 1024);
                var y = token.GetIntValueOnly("height", 1024);
                return new Size(x, y);
            }

            var v = token.ToString().AsInt(1024);
            return new Size(v, v);
        }

        private static PaintShopPatternColorReference GetPatternColorReference(JToken t) {
            if (t == null || t.Type == JTokenType.Null || t.Type == JTokenType.None) return default(PaintShopPatternColorReference);
            var s = t.ToString();
            return s.StartsWith("#") ? new PaintShopPatternColorReference(s.ToColor()?.ToColor()) : new PaintShopPatternColorReference(s.AsInt(0));
        }

        private static FontWeight GetFontWeight(JToken t) {
            if (t == null || t.Type == JTokenType.Null || t.Type == JTokenType.None) return FontWeight.Normal;
            var s = t.ToString();
            return Enum.TryParse(s, true, out FontWeight v) ? v : s.AsInt((int)FontWeight.Normal).FindClosest<FontWeight>();
        }

        private static FontStyle GetFontStyle(JToken t) {
            if (t == null || t.Type == JTokenType.Null || t.Type == JTokenType.None) return FontStyle.Normal;
            var s = t.ToString();
            return Enum.TryParse(s, true, out FontStyle v) ? v : s.AsInt((int)FontStyle.Normal).FindClosest<FontStyle>();
        }

        private static FontStretch GetFontStretch(JToken t) {
            if (t == null || t.Type == JTokenType.Null || t.Type == JTokenType.None) return FontStretch.Normal;
            var s = t.ToString();
            return Enum.TryParse(s, true, out FontStretch v) ? v : s.AsInt((int)FontStretch.Normal).FindClosest<FontStretch>();
        }

        [NotNull]
        private static PaintShopFontSource GetFont(JToken j, [NotNull] ReferenceSolver refSolver) {
            var s = j?.ToString() ?? "Arial";
            if (s.StartsWith("./") || s.StartsWith(".\\")) {
                var data = refSolver.GetData(s.Substring(2));
                return data == null ? PaintShopFontSource.CreateDefault() : PaintShopFontSource.FromMemory(data);
            }

            return PaintShopFontSource.FromFamilyName(s);
        }

        private static PaintShopAlignment GetAlignment(JToken j) {
            var s = j?.ToString() ?? "center";

            switch (s) {
                case "left":
                case "top":
                case "begin":
                case "start":
                case "-1":
                    return PaintShopAlignment.Start;
                case "right":
                case "bottom":
                case "end":
                case "finish":
                case "1":
                case "+1":
                    return PaintShopAlignment.End;
                default:
                    return PaintShopAlignment.Center;
            }
        }

        [CanBeNull]
        private static PaintShopPatternNumbers GetNumbers(JToken j, [NotNull] ReferenceSolver refSolver) {
            return j is JObject o ? new PaintShopPatternNumbers(
                    o.GetDoubleValueOnly(KeySize, 200d),
                    o.GetDoubleValueOnly(KeyLeft, 200d),
                    o.GetDoubleValueOnly(KeyTop, 200d),
                    GetAlignment(o[KeyHorizontalAlignment]),
                    GetAlignment(o[KeyVerticalAlignment]),
                    GetFont(o[KeyFont], refSolver),
                    o.GetDoubleValueOnly(KeyAngle, 200d),
                    GetPatternColorReference(o[KeyColor]),
                    GetFontWeight(o[KeyWeight]),
                    GetFontStyle(o[KeyStyle]),
                    GetFontStretch(o[KeyStretch])) : null;
        }

        [CanBeNull]
        private static PaintShopSource GetSource([CanBeNull] JToken j, [NotNull] ReferenceSolver refSolver, [CanBeNull] PaintShopSourceParams baseParams) {
            if (j == null) return null;

            if (j.Type == JTokenType.Object) {
                var o = (JObject)j;
                baseParams = GetSourceParams(o);

                if (o[KeyRed] != null) {
                    return new PaintShopSource(
                            GetSource(o[KeyRed], refSolver, baseParams),
                            GetSource(o[KeyGreen], refSolver, baseParams),
                            GetSource(o[KeyBlue], refSolver, baseParams),
                            GetSource(o[KeyAlpha], refSolver, baseParams)).SetFrom(baseParams);
                }

                return GetSource(o["path"], refSolver, baseParams);
            }

            if (j.Type == JTokenType.Boolean) {
                var b = (bool)j;
                return b ? PaintShopSource.InputSource.SetFrom(baseParams) : null;
            }

            if (j.Type != JTokenType.String) {
                return null;
            }

            var s = j.ToString();
            string c = null;

            var index = s.IndexOf(':');
            if (index > 1) {
                c = s.Substring(index + 1);
                s = s.Substring(0, index);
            }

            if (s == "@self" || s == "#self") {
                return PaintShopSource.InputSource.SetFrom(baseParams).MapChannels(c);
            }

            if (s.StartsWith("@ref:") || s.StartsWith("#ref:")) {
                var r = s.Substring(5);
                return new PaintShopSource(refSolver.GetColorReference(r));
            }

            if (s.StartsWith("#")) {
                return new PaintShopSource(s.ToColor()?.ToColor() ?? System.Drawing.Color.White)
                        .SetFrom(baseParams).MapChannels(c);
            }

            if (s.StartsWith("./") || s.StartsWith(".\\")) {
                var data = refSolver.GetData(s.Substring(2));
                return data == null ? PaintShopSource.Transparent : new PaintShopSource(data).SetFrom(baseParams).MapChannels(c);
            }

            return new PaintShopSource(s)
                    .SetFrom(baseParams).MapChannels(c);
        }

        private const string TypeColor = "color";
        private const string TypeCarPaint = "carpaint";
        private const string TypePattern = "pattern";
        private const string TypeTintedWindow = "tintedwindow";
        private const string TypeLicensePlate = "licenseplate";
        private const string TypeSolidColorIfFlagged = "solidcolorifflagged";
        private const string TypeTransparentIfFlagged = "transparentifflagged";
        private const string TypeReplacedIfFlagged = "replacedifflagged";
        private const string TypeReplacement = "replacement";

        private const string KeyId = "id";
        private const string KeyName = "name";
        private const string KeyEnabled = "enabled";
        private const string KeyInverse = "inverse";
        private const string KeyType = "type";
        private const string KeyStyle = "style";
        private const string KeyTexture = "texture";
        private const string KeyTextures = "textures";
        private const string KeyNormalsTexture = "normals";
        private const string KeyMapsDefault = "mapsTexture";
        private const string KeyMapsMaskTexture = "mapsMask";
        private const string KeyMapsDefaultTexture = "mapsDefaultTexture";
        private const string KeyColor = "color";
        private const string KeyColors = "colors";
        private const string KeyOpacity = "opacity";
        private const string KeyDefaultColor = "defaultColor";
        private const string KeyDefaultColors = "defaultColors";
        private const string KeyDefaultOpacity = "defaultOpacity";
        private const string KeyFixedColor = "fixedColor";
        private const string KeyTintBase = "tintBase";
        private const string KeyPairs = "pairs";
        private const string KeyPattern = "pattern";
        private const string KeySize = "size";
        private const string KeyLeft = "left";
        private const string KeyTop = "top";
        private const string KeyHorizontalAlignment = "horizontalAlignment";
        private const string KeyVerticalAlignment = "verticalAlignment";
        private const string KeyAngle = "angle";
        private const string KeyWeight = "weight";
        private const string KeyStretch = "stretch";
        private const string KeyFont = "font";
        private const string KeyRed = "red";
        private const string KeyGreen = "green";
        private const string KeyBlue = "blue";
        private const string KeyAlpha = "alpha";
        private const string KeyBase = "base";
        private const string KeyPatternBase = "patternBase";
        private const string KeyPatternOverlay = "patternOverlay";
        private const string KeyPatternTexture = "patternTexture";
        private const string KeyPatternSize = "patternSize";
        private const string KeyOverlay = "overlay";
        private const string KeyPatterns = "patterns";
        private const string KeyNumbers = "numbers";
        private const string KeySource = "source";
        private const string KeyMask = "mask";
        private const string KeyAutoLevel = "autoLevel";
        private const string KeyDesaturate = "desaturate";
        private const string KeyMapsAutoLevel = "mapsAutoLevel";
        private const string KeyMapsDesaturate = "mapsDesaturate";
        private const string KeyAllowed = "allowed";
        private const string KeyAllowedColors = "allowedColors";
        private const string KeyFlakesSize = "flakesSize";
        private const string KeyColored = "colored";
        private const string KeyColorAvailable = "colorAvailable";
        private const string KeyFlakesAvailable = "flakesAvailable";
        private const string KeyCandidates = "candidates";
        private const string KeyLiveryStyle = "liveryStyle";
        private const string KeyLiveryPriority = "liveryPriority";
        private const string KeyLiveryColor = "liveryColor";
        private const string KeyLiveryColors = "liveryColors";

        [NotNull]
        private static Dictionary<string, PaintShopSource> GetNameSourcePairs(JToken t, [NotNull] ReferenceSolver refSolver, PaintShopSourceParams sourceParams) {
            return t?.ToObject<Dictionary<string, JToken>>().Select(x => new {
                x.Key,
                Source = GetSource(x.Value, refSolver, sourceParams)
            }).Where(x => x.Source != null).ToDictionary(
                    x => x.Key,
                    x => x.Source) ?? new Dictionary<string, PaintShopSource>();
        }

        [NotNull]
        private static Dictionary<TextureFileName, PaintShopSource> GetTextureFileNameSourcePairs(JToken t, [NotNull] ReferenceSolver refSolver,
                PaintShopSourceParams sourceParams) {
            return t?.ToObject<Dictionary<string, JToken>>().Select(x => new {
                x.Key,
                Source = GetSource(x.Value, refSolver, sourceParams)
            }).Where(x => x.Source != null).ToDictionary(
                    x => new TextureFileName(x.Key),
                    x => x.Source) ?? new Dictionary<TextureFileName, PaintShopSource>();
        }

        /// <summary>
        /// Load entries from pairs table: name (or KN5’s texture) → replacement.
        /// </summary>
        [NotNull]
        private static Dictionary<string, PaintShopSource> GetNameSourcePairs(JObject e, string key, [NotNull] ReferenceSolver refSolver) {
            return GetNameSourcePairs(e[key], refSolver, GetSourceParams(e));
        }

        /// <summary>
        /// Specially for car paint, basically it’s PaintShopSource with an extra property.
        /// </summary>
        /// <returns></returns>
        [NotNull]
        private static Dictionary<string, CarPaintReplacementSource> GetNameSourcePairs(JToken t, [NotNull] ReferenceSolver refSolver) {
            return t?.ToObject<Dictionary<string, JToken>>().Select(x => new {
                x.Key,
                Replacement = new CarPaintReplacementSource(GetSource(x.Value, refSolver, null), (x.Value as JObject)?.GetBoolValueOnly(KeyColored) ?? false)
            }).Where(x => x.Replacement.Source != null).ToDictionary(
                    x => x.Key,
                    x => x.Replacement) ?? new Dictionary<string, CarPaintReplacementSource>();
        }

        /// <summary>
        /// Load entries from pairs table: result texture name → replacement.
        /// </summary>
        [NotNull]
        private static Dictionary<TextureFileName, PaintShopSource> GetTextureFileNameSourcePairs(JObject e, string key, [NotNull] ReferenceSolver refSolver) {
            return GetTextureFileNameSourcePairs(e[key], refSolver, GetSourceParams(e));
        }

        /// <summary>
        /// Load entries from pairs table: name (or KN5’s texture) → KN5’s texture → replacement.
        /// </summary>
        [NotNull]
        private static Dictionary<string, Dictionary<TextureFileName, PaintShopSource>> GetNameNameSourcePairs(JObject e, string key,
                [NotNull] ReferenceSolver refSolver) {
            var sourceParams = GetSourceParams(e);
            return e[key]?.ToObject<Dictionary<string, JToken>>().ToDictionary(x => x.Key, x => GetTextureFileNameSourcePairs(x.Value, refSolver, sourceParams))
                    ?? new Dictionary<string, Dictionary<TextureFileName, PaintShopSource>>();
        }

        public class TintedEntry {
            public TintedEntry([NotNull] PaintShopSource source, [CanBeNull] PaintShopSource mask, [CanBeNull] PaintShopSource overlay) {
                Source = source;
                Mask = mask;
                Overlay = overlay;
            }

            [NotNull]
            public PaintShopSource Source { get; }

            [CanBeNull]
            public PaintShopSource Mask { get; }

            [CanBeNull]
            public PaintShopSource Overlay { get; }
        }

        /// <summary>
        /// Similar to GetTextureSourcePairs, but also loads masks and have a fallback.
        /// </summary>
        [CanBeNull]
        private static Dictionary<TextureFileName, TintedEntry> GetTintedPairs(JObject e, [NotNull] ReferenceSolver refSolver) {
            var sourceParams = GetSourceParams(e);
            return e[KeyPairs]?.ToObject<Dictionary<string, JToken>>().Select(x => new {
                x.Key,
                Source = GetSource((x.Value as JObject)?[KeySource] ?? x.Value, refSolver, sourceParams),
                Mask = GetSource((x.Value as JObject)?[KeyMask], refSolver, null),
                Overlay = GetSource((x.Value as JObject)?[KeyOverlay], refSolver, null),
            }).Where(x => x.Source != null).ToDictionary(
                    x => new TextureFileName(x.Key),
                    x => new TintedEntry(x.Source, x.Mask, x.Overlay)) ??
                    (e.GetBoolValueOnly(KeyTintBase) == true || sourceParams.RequiresPreparation ? new Dictionary<TextureFileName, TintedEntry> {
                        [new TextureFileName(RequireString(e, KeyTexture))] = new TintedEntry(PaintShopSource.InputSource.SetFrom(sourceParams), null, null)
                    } : new Dictionary<TextureFileName, TintedEntry> {
                        [new TextureFileName(RequireString(e, KeyTexture))] = new TintedEntry(PaintShopSource.Transparent.SetFrom(sourceParams), null, null)
                    });
        }

        /// <summary>
        /// Returns either texture or textures.
        /// </summary>
        [NotNull]
        private static TextureFileName[] GetTextures(JObject e) {
            return e[KeyTextures]?.ToObject<string[]>().Select(x => new TextureFileName(x)).ToArray() ??
                    new[] { new TextureFileName(RequireString(e, KeyTexture)) };
        }

        private static Dictionary<string, Color> GetAllowedColors(JObject e, string key) {
            var s = e[key]?.ToObject<Dictionary<string, string>>();
            if (s == null) return null;

            var dictionary = new Dictionary<string, Color>();
            foreach (var pair in s) {
                var color = pair.Value?.ToColor();
                if (color.HasValue) {
                    dictionary.Add(pair.Key, color.Value);
                }
            }

            return dictionary;
        }

        private static CarPaintColors GetColors([NotNull] JObject parent, Color? defaultColor = null) {
            var allowedColors = GetAllowedColors(parent, KeyAllowedColors);

            var actualDefaultColor = parent[KeyDefaultColor]?.ToString().ToColor() ?? defaultColor ?? Colors.White;
            if (parent[KeyDefaultColors] is JArray colors) {
                var list = new List<CarPaintColor>(colors.Count);
                for (var i = 0; i < colors.Count; i++) {
                    var defaultName = $"Color #{i + 1}";
                    var entry = colors[i];
                    if (entry.Type == JTokenType.Object) {
                        var jEntry = (JObject)entry;
                        var localAllowedColors = GetAllowedColors(jEntry, KeyAllowed) ?? allowedColors;
                        list.Add(new CarPaintColor(jEntry.GetStringValueOnly(KeyName) ?? defaultName, GetColor(jEntry, KeyColor, actualDefaultColor),
                                localAllowedColors));
                    } else {
                        list.Add(new CarPaintColor(defaultName, GetColor(entry, actualDefaultColor), allowedColors));
                    }
                }

                return new CarPaintColors(list.ToArray());
            }

            var colorsCount = parent.GetIntValueOnly(KeyColors);
            return new CarPaintColors(colorsCount.HasValue ?
                    Enumerable.Range(1, colorsCount.Value).Select(x => new CarPaintColor($"Color #{x}", actualDefaultColor, allowedColors)).ToArray() :
                    new[] { new CarPaintColor("Color", actualDefaultColor, allowedColors) });
        }

        [CanBeNull]
        private static int[] GetLiveryColorIds(JObject obj) {
            var liveryColor = obj.GetIntValueOnly(KeyLiveryColor);
            if (liveryColor.HasValue) {
                var liveryColorValue = liveryColor.Value.Clamp(0, 10);
                var result = new int[liveryColorValue + 1];
                for (var j = 0; j < result.Length; j++) {
                    result[j] = -1;
                }
                result[liveryColorValue] = 0;
                return result;
            }

            try {
                return obj[KeyLiveryColors]?.ToObject<int[]>();
            } catch (Exception e) {
                Logging.Warning(e.Message);
            }

            return null;
        }

        [CanBeNull]
        private static CarPaintPattern GetPattern(JObject obj, [NotNull] ReferenceSolver refSolver, Size? size) {
            var sourceParams = GetSourceParams(obj);
            var name = obj.GetStringValueOnly(KeyName);
            var pattern = GetSource(obj[KeyPattern], refSolver, sourceParams);
            var overlay = GetSource(obj[KeyOverlay], refSolver, sourceParams);

            var numbersToken = obj[KeyNumbers];
            var numbersArray = numbersToken as JArray;
            var numbers = numbersArray?.OfType<JObject>().Select(x => GetNumbers(x, refSolver)) ?? new[]{ GetNumbers(numbersToken, refSolver) };
            return pattern == null ? null : new CarPaintPattern(name, pattern, overlay, GetSize(obj[KeySize]) ?? size, GetColors(obj), numbers) {
                LiveryStyle = obj.GetStringValueOnly(KeyLiveryStyle),
                LiveryColorIds = GetLiveryColorIds(obj)
            };
        }

        [CanBeNull]
        private static PaintableItem GetPaintableItem([NotNull] JObject e, [NotNull] ReferenceSolver refSolver) {
            PaintableItem result;
            var type = GetString(e, KeyType, TypeColor).ToLowerInvariant();
            switch (type) {
                case TypePattern:
                    var sourceParams = GetSourceParams(e);
                    var size = GetSize(e[KeyPatternSize]) ?? GetSize(e[KeySize]);
                    result = new TexturePattern(
                            new TextureFileName(e.GetStringValueOnly(KeyPatternTexture) ?? RequireString(e, KeyTexture)),
                            GetSource(e[KeyPatternBase], refSolver, sourceParams) ?? GetSource(e[KeyPatternTexture], refSolver, sourceParams) ??
                                    GetSource(e[KeyBase], refSolver, sourceParams) ?? GetSource(e[KeyTexture], refSolver, sourceParams),
                            GetSource(e[KeyPatternOverlay], refSolver, null),
                            e[KeyPatterns].OfType<JObject>().Select(x => GetPattern(x, refSolver, size)).NonNull()) {
                                LiveryColorIds = GetLiveryColorIds(e)
                            };
                    break;
                case TypeCarPaint:
                    var maps = GetString(e, KeyMapsDefault);
                    CarPaint carPaint;
                    if (maps != null) {
                        var mapsSourceParams = GetMapsSourceParams(e);
                        var mapsSource = GetSource(e[KeyMapsDefaultTexture], refSolver, mapsSourceParams) ??
                                PaintShopSource.InputSource.SetFrom(mapsSourceParams);
                        var mapsMask = GetSource(e[KeyMapsMaskTexture], refSolver, null);
                        carPaint = new ComplexCarPaint(new TextureFileName(maps), mapsSource, mapsMask);
                    } else {
                        carPaint = new CarPaint();
                    }

                    carPaint.SetDetailsParams(
                            new TextureFileName(RequireString(e, KeyTexture)),
                            e.GetBoolValueOnly(KeyFlakesAvailable) ?? true,
                            e.GetIntValueOnly(KeyFlakesSize) ?? 512,
                            e.GetBoolValueOnly(KeyColorAvailable) ?? true,
                            GetColor(e, KeyDefaultColor),
                            GetNameSourcePairs(e[KeyCandidates], refSolver));

                    var paintPatternTexture = e.GetStringValueOnly(KeyPatternTexture);
                    if (paintPatternTexture != null) {
                        var patternSize = GetSize(e[KeyPatternSize]);
                        var patternBase = GetSource(e[KeyPatternBase], refSolver, GetSourceParams(e)) ??
                                GetSource(e[KeyPatternTexture], refSolver, GetSourceParams(e));
                        var patternOverlay = GetSource(e[KeyPatternOverlay], refSolver, null);
                        var patterns = (e[KeyPatterns] as JArray)?.OfType<JObject>().Select(x => GetPattern(x, refSolver, patternSize)).NonNull();
                        if (patternBase != null && patterns != null) {
                            carPaint.SetPatterns(new TextureFileName(paintPatternTexture), patternBase, patternOverlay, patterns);
                        }
                    }

                    carPaint.LiveryStyle = e.GetStringValueOnly(KeyLiveryStyle) ?? "Flat";
                    carPaint.GuessColorsFromPreviews = e.GetBoolValueOnly("guessColorsFromPreviews") ?? false;
                    result = carPaint;
                    break;
                case TypeColor:
                    result = new ColoredItem(GetTintedPairs(e, refSolver), GetColors(e)) {
                        LiveryColorIds = GetLiveryColorIds(e)
                    };
                    break;
                case TypeTintedWindow:
                    result = new TintedWindows(GetTintedPairs(e, refSolver),
                            GetColors(e, Color.FromRgb(41, 52, 55)),
                            GetDouble(e, KeyDefaultOpacity, 0.23),
                            e.GetBoolValueOnly(KeyFixedColor) ?? false) {
                                LiveryColorIds = GetLiveryColorIds(e)
                            };
                    break;
                case TypeLicensePlate:
                    result = new LicensePlate(GetString(e, KeyStyle, "Europe"), GetString(e, KeyTexture, "Plate_D.dds"),
                            GetString(e, KeyNormalsTexture, "Plate_NM.dds"));
                    break;
                case TypeSolidColorIfFlagged:
                    result = new SolidColorIfFlagged(GetTextures(e), e.GetBoolValueOnly(KeyInverse) ?? false,
                            GetColor(e, KeyColor), GetDouble(e, KeyOpacity, 0.23));
                    break;
                case TypeTransparentIfFlagged:
                    result = new TransparentIfFlagged(GetTextures(e), e.GetBoolValueOnly(KeyInverse) ?? false);
                    break;
                case TypeReplacedIfFlagged:
                    result = new ReplacedIfFlagged(e.GetBoolValueOnly(KeyInverse) ?? false, GetTextureFileNameSourcePairs(e, KeyPairs, refSolver));
                    break;
                case TypeReplacement:
                    try {
                        result = new Replacement(GetTextures(e), GetNameSourcePairs(e, KeyCandidates, refSolver));
                    } catch (MissingValueException ex) when (ex.Key == KeyTexture) {
                        result = new MultiReplacement(GetNameNameSourcePairs(e, KeyCandidates, refSolver));
                    }
                    break;
                default:
                    throw new Exception($"Not supported type: {type}");
            }

            var name = GetString(e, KeyName);
            if (name != null) {
                result.DisplayName = name;
            }

            var enabled = e.GetBoolValueOnly(KeyEnabled);
            if (enabled.HasValue) {
                result.Enabled = enabled.Value;
            }

            var liveryPriority = e.GetIntValueOnly(KeyLiveryPriority);
            if (liveryPriority.HasValue) {
                result.LiveryPriority = liveryPriority.Value;
            }

            return result;
        }

        private static List<PaintableItem> GetJArrayPaintableItems(JArray array, [CanBeNull] Kn5 kn5, [NotNull] List<string> previousIds,
                [NotNull] ReferenceSolver refSolver) {
            var result = new List<PaintableItem>();
            foreach (var item in array) {
                if (item.Type == JTokenType.String) {
                    var s = (string)item;
                    if (!s.StartsWith("@")) {
                        var inherited = GetCarPaintableItems(s, kn5, previousIds);
                        if (inherited != null) {
                            result.AddRange(inherited);
                        }
                    } else if (string.Equals(s, "@guess", StringComparison.OrdinalIgnoreCase)) {
                        result.AddRange(GuessPaintableItems(kn5));
                    }
                } else {
                    if (item is JObject o) {
                        try {
                            var i = GetPaintableItem(o, refSolver);
                            if (i != null) {
                                result.Add(i);
                            }
                        } catch (Exception e) {
                            Logging.Error(e);
                        }
                    } else {
                        Logging.Warning("Unknown entry: " + item);
                    }
                }
            }
            return result;
        }

        private static IEnumerable<PaintableItem> GetJArrayPaintableItems(JArray array, [CanBeNull] Kn5 kn5, [NotNull] List<string> previousIds, string filename) {
            var refSolver = new ReferenceSolver();
            using (refSolver.SetDataProvider(filename)) {
                var list = GetJArrayPaintableItems(array, kn5, previousIds, refSolver);
                refSolver.SetRefList(list);
                return list;
            }
        }

        private static IEnumerable<PaintableItem> GetDownloadedPaintableItems(string downloadedData, string carId, [CanBeNull] Kn5 kn5,
                [NotNull] List<string> previousIds) {
            var refSolver = new ReferenceSolver();
            using (var zip = ZipFile.OpenRead(downloadedData))
            using (refSolver.SetDataProvider(zip)) {
                var manifest = zip.GetEntry("Manifest.json").Open().ReadAsStringAndDispose();
                var jObj = JObject.Parse(manifest);
                if (jObj.GetStringValueOnly("id") != carId) {
                    throw new Exception($"ID is wrong: {jObj.GetStringValueOnly("id")}≠{carId}");
                }

                var list = GetJArrayPaintableItems((JArray)jObj["entries"], kn5, previousIds, refSolver);
                refSolver.SetRefList(list);
                return list;
            }
        }

        [CanBeNull]
        private static IEnumerable<PaintableItem> GetCarPaintableItems(string carId, [CanBeNull] Kn5 kn5, [NotNull] List<string> previousIds) {
            var carIdLower = carId.ToLowerInvariant();
            if (previousIds.Contains(carIdLower)) return new PaintableItem[0];
            previousIds.Add(carIdLower);

            var car = CarsManager.Instance.GetById(carId);
            var candidate = car == null ? null : Path.Combine(car.Location, "ui", "cm_paintshop.json");
            if (car != null && File.Exists(candidate)) {
                try {
                    var t = JToken.Parse(File.ReadAllText(candidate));
                    var j = (t as JObject)?.GetValue(carId, StringComparison.OrdinalIgnoreCase) as JArray ?? t as JArray;
                    if (j != null) {
                        return GetJArrayPaintableItems(j, kn5, previousIds, candidate);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            } else {
                foreach (var filename in FilesStorage.Instance.GetContentFilesFiltered(@"*.json", ContentCategory.PaintShop).Select(x => x.Filename)) {
                    try {
                        var j = JObject.Parse(File.ReadAllText(filename));
                        if (j.GetValue(carId, StringComparison.OrdinalIgnoreCase) is JArray d) {
                            return GetJArrayPaintableItems(d, kn5, previousIds, filename);
                        }
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                }
            }

            return null;
        }

        [ItemCanBeNull]
        private static async Task<IEnumerable<PaintableItem>> GetCarPaintableItemsAsync(string carId, [CanBeNull] Kn5 kn5, [NotNull] List<string> previousIds,
                [CanBeNull] Func<CancellationToken, Task<string>> remoteDataFallback, bool guessFallback, CancellationToken cancellation) {
            var result = await Task.Run(() => GetCarPaintableItems(carId, kn5, previousIds));
            if (result != null || cancellation.IsCancellationRequested) {
                return result;
            }

            if (remoteDataFallback != null) {
                try {
                    var remoteDataLoaded = await remoteDataFallback.Invoke(cancellation);
                    if (cancellation.IsCancellationRequested) return null;

                    if (remoteDataLoaded != null) {
                        return await Task.Run(() => GetDownloadedPaintableItems(remoteDataLoaded, carId, kn5, previousIds));
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            return guessFallback ? GuessPaintableItems(kn5) : new PaintableItem[0];
        }

        private class PaintableItemComparer : IComparer<PaintableItem>, IEqualityComparer<PaintableItem> {
            internal static readonly PaintableItemComparer Instance = new PaintableItemComparer();

            public int Compare(PaintableItem x, PaintableItem y) {
                var c = string.CompareOrdinal(x?.DisplayName ?? "", y?.DisplayName ?? "");
                return c == 0 ? x?.Guessed == true ? y?.Guessed == true ? 0 : 1 : -1 : c;
            }

            public bool Equals(PaintableItem x, PaintableItem y) {
                return x?.DisplayName == y?.DisplayName;
            }

            public int GetHashCode(PaintableItem obj) {
                return obj?.DisplayName.GetHashCode() ?? -1;
            }
        }

        [ItemCanBeNull]
        public static async Task<List<PaintableItem>> GetPaintableItemsAsync(string carId, [CanBeNull] Kn5 kn5, CancellationToken cancellation) {
            if (!PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId)) return new List<PaintableItem>(0);

            try {
                var result = (await GetCarPaintableItemsAsync(carId, kn5, new List<string>(2),
                        async c => (await CmApiProvider.GetPaintShopDataAsync(carId, cancellation: c))?.Item1, true, cancellation))?
                        .Reverse().Distinct(PaintableItemComparer.Instance).ToList();
                if (result == null || cancellation.IsCancellationRequested) return null;

                result.Sort(PaintableItemComparer.Instance);
                return result;
            } catch (Exception e) when (e.IsCanceled()) {
                return null;
            }
        }
    }
}