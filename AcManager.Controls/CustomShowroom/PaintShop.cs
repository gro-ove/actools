using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Controls.CustomShowroom {
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

        private static IEnumerable<PaintableItem> GuessPaintableItemsInner([CanBeNull] Kn5 kn5) {
            if (kn5 == null) yield break;

            var carPaint = new[] { "Metal_detail.dds", "carpaint_detail.dds", "metal_detail.dds", "car_paint.dds", "carpaint.dds" }
                    .FirstOrDefault(x => kn5.Textures.ContainsKey(x));
            var mapsMap = kn5.Materials.Values.Where(x => x.ShaderName == "ksPerPixelMultiMap_damage_dirt")
                             .Select(x => x.GetMappingByName(@"txMaps")?.Texture)
                             .NonNull()
                             .FirstOrDefault();

            if (kn5.Textures.ContainsKey("Plate_D.dds") && kn5.Textures.ContainsKey("Plate_NM.dds")) {
                yield return new LicensePlate(LicensePlate.LicenseFormat.Europe);
            }

            if (carPaint != null) {
                yield return mapsMap == null ? new CarPaint(carPaint) : new ComplexCarPaint(carPaint, mapsMap) {
                    AutoAdjustLevels = true
                };
            }

            var rims = new[] { "car_paint_rims.dds" }
                    .Where(x => kn5.Textures.ContainsKey(x))
                    .Select(x => new ColoredItem(x, Colors.AliceBlue) { DisplayName = "Rims", Enabled = false })
                    .FirstOrDefault();
            if (rims != null) yield return rims;

            var rollCage = new[] { "car_paint_roll_cage.dds" }
                    .Where(x => kn5.Textures.ContainsKey(x))
                    .Select(x => new ColoredItem(x, Colors.AliceBlue) { DisplayName = "Roll cage", Enabled = false })
                    .FirstOrDefault();
            if (rollCage != null) yield return rollCage;

            var glass = new[] { "ext_glass.dds" }
                    .Where(x => kn5.Textures.ContainsKey(x))
                    .Select(x => new TintedWindows(x) { Enabled = false })
                    .FirstOrDefault();
            if (glass != null) yield return glass;
        }

        private static IEnumerable<PaintableItem> GuessPaintableItems([CanBeNull] Kn5 kn5) {
            foreach (var item in GuessPaintableItemsInner(kn5)) {
                item.Guessed = true;
                yield return item;
            }
        }

        [CanBeNull, ContractAnnotation(@"defaultValue: notnull => notnull")]
        private static string GetString(JObject j, string key, string defaultValue = null) {
            return j.GetStringValueOnly(key) ?? defaultValue;
        }

        [NotNull]
        private static string RequireString(JObject j, string key) {
            var s = j.GetStringValueOnly(key);
            if (s == null) {
                throw new Exception($"Value required: “{key}”");
            }
            return s;
        }
        
        private static Color GetColor(JObject j, string key, Color? defaultColor = null) {
            var s = j.GetStringValueOnly(key);
            return (s == null ? defaultColor : s.ToColor() ?? defaultColor) ?? Colors.White;
        }
        
        private static double GetDouble(JObject j, string key, double defaultValue = 0d) {
            var s = j.GetStringValueOnly(key);
            return s == null ? defaultValue : FlexibleParser.TryParseDouble(s) ?? defaultValue;
        }

        [CanBeNull]
        private static PaintShopSource GetSource(JToken j, Func<string, byte[]> extraData) {
            if (j == null) return null;

            if (j.Type == JTokenType.Boolean) {
                var b = (bool)j;
                return b ? PaintShopSource.InputSource : null;
            }

            if (j.Type != JTokenType.String) {
                return null;
            }

            var s = j.ToString();
            if (s == "@self" || s == "#self") {
                return PaintShopSource.InputSource;
            }

            if (s.StartsWith("#")) {
                return new PaintShopSource(s.ToColor()?.ToColor() ?? System.Drawing.Color.White);
            }

            if (s.StartsWith("./") || s.StartsWith(".\\")) {
                return new PaintShopSource(extraData(s.Substring(2)));
            }

            return new PaintShopSource(s);
        }

        private const string TypeColor = "color";
        private const string TypeCarPaint = "carpaint";
        private const string TypeTintedWindow = "tintedwindow";
        private const string TypeLicensePlate = "licenseplate";
        private const string TypeSolidColorIfFlagged = "solidcolorifflagged";
        private const string TypeTransparentIfFlagged = "transparentifflagged";
        private const string TypeReplacedIfFlagged = "replacedifflagged";

        private const string KeyName = "name";
        private const string KeyEnabled = "enabled";
        private const string KeyInverse = "inverse";
        private const string KeyType = "type";
        private const string KeyStyle = "style";
        private const string KeyTexture = "texture";
        private const string KeyTextures = "textures";
        private const string KeyNormalsTexture = "normals";
        private const string KeyMapsDefault = "mapsTexture";
        private const string KeyMapsDefaultTexture = "mapsDefaultTexture";
        private const string KeyMapsAutoLevel = "mapsAutoLevel";
        private const string KeyColor = "color";
        private const string KeyOpacity = "opacity";
        private const string KeyDefaultColor = "defaultColor";
        private const string KeyDefaultOpacity = "defaultOpacity";
        private const string KeyTintBase = "tintBase";
        private const string KeyAutoLevel = "autoLevel";
        private const string KeyPairs = "pairs";

        /// <summary>
        /// Load entries from pairs table: KN5’s texture → replacement.
        /// </summary>
        [CanBeNull]
        private static Dictionary<string, PaintShopSource> GetTextureSourcePairs(JObject e, Func<string, byte[]> extraData) {
            return e[KeyPairs]?.ToObject<Dictionary<string, JToken>>().Select(x => new {
                x.Key,
                Source = GetSource(x.Value, extraData)
            }).Where(x => x.Source != null).ToDictionary(
                    x => x.Key,
                    x => x.Source);
        }

        /// <summary>
        /// Similar to GetTextureSourcePairs, but with a fallback.
        /// </summary>
        [CanBeNull]
        private static Dictionary<string, PaintShopSource> GetTintedPairs(JObject e, Func<string, byte[]> extraData) {
            return GetTextureSourcePairs(e, extraData) ?? (e.GetBoolValueOnly(KeyTintBase) == true ? new Dictionary<string, PaintShopSource> {
                [RequireString(e, KeyTexture)] = PaintShopSource.InputSource
            } : new Dictionary<string, PaintShopSource> {
                [RequireString(e, KeyTexture)] = PaintShopSource.White
            });
        }

        /// <summary>
        /// Returns either texture or textures.
        /// </summary>
        [CanBeNull]
        private static string[] GetTextures(JObject e) {
            return e[KeyTextures]?.ToObject<string[]>() ?? new[] { RequireString(e, KeyTexture) };
        }

        [CanBeNull]
        private static PaintableItem GetPaintableItem([NotNull] JObject e, Func<string, byte[]> extraData) {
            PaintableItem result;
            var type = GetString(e, KeyType, TypeColor).ToLowerInvariant();
            switch (type) {
                case TypeCarPaint:
                    var maps = GetString(e, KeyMapsDefault);
                    CarPaint carPaint;
                    if (maps != null) {
                        carPaint = new ComplexCarPaint(RequireString(e, KeyTexture), maps, GetColor(e, KeyDefaultColor)) {
                            AutoAdjustLevels = e.GetBoolValueOnly(KeyMapsAutoLevel) ?? false,
                            MapsDefaultTexture = GetSource(e[KeyMapsDefaultTexture], extraData),
                        };
                    } else {
                        carPaint = new CarPaint(RequireString(e, KeyTexture), GetColor(e, KeyDefaultColor));
                    }

                    var patternTexture = e.GetStringValueOnly("patternTexture");
                    if (patternTexture != null) {
                        var patternBase = GetSource(e["patternBase"], extraData);
                        var patternOverlay = GetSource(e["patternOverlay"], extraData);
                        var patterns = (e["patterns"] as JArray)?.Select(x =>
                                new CarPaintPattern(
                                        x.GetStringValueOnly(KeyName),
                                        GetSource(x["pattern"], extraData),
                                        GetSource(x["overlay"], extraData),
                                        x.GetIntValueOnly("colors") ?? 0));
                        if (patternBase != null && patterns != null) {
                            carPaint.SetPatterns(patternTexture, patternBase, patternOverlay, patterns);
                        }
                    }

                    result = carPaint;
                    break;
                case TypeColor:
                    result = new ColoredItem(GetTintedPairs(e, extraData), GetColor(e, KeyDefaultColor)) {
                        AutoAdjustLevels = e.GetBoolValueOnly(KeyAutoLevel) ?? false
                    };
                    break;
                case TypeTintedWindow:
                    result = new TintedWindows(GetTintedPairs(e, extraData),
                            GetDouble(e, KeyDefaultOpacity, 0.23),
                            GetColor(e, KeyDefaultColor, Color.FromRgb(41, 52, 55))) {
                                AutoAdjustLevels = e.GetBoolValueOnly(KeyAutoLevel) ?? false
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
                    result = new ReplacedIfFlagged(e.GetBoolValueOnly(KeyInverse) ?? false, GetTextureSourcePairs(e, extraData));
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

            return result;
        }

        private static IEnumerable<PaintableItem> GetPaintableItems(JArray array, [CanBeNull] Kn5 kn5, [NotNull] List<string> previousIds, string filename) {
            var result = new List<PaintableItem>();
            ZipArchive[] data = { null };

            try {
                foreach (var item in array) {
                    if (item.Type == JTokenType.String) {
                        var s = (string)item;
                        if (!s.StartsWith("@")) {
                            result.AddRange(GetPaintableItems(s, kn5, previousIds, false));
                        } else if (string.Equals(s, "@guess", StringComparison.OrdinalIgnoreCase)) {
                            result.AddRange(GuessPaintableItems(kn5));
                        }
                    } else {
                        var o = item as JObject;
                        if (o == null) {
                            Logging.Warning("Unknown entry: " + item);
                        } else {
                            try {
                                var i = GetPaintableItem(o, s => {
                                    var unpackedFilename = Path.Combine(filename.ApartFromLast(@".json", StringComparison.OrdinalIgnoreCase), s);
                                    if (File.Exists(unpackedFilename)) {
                                        return File.ReadAllBytes(unpackedFilename);
                                    }

                                    if (data[0] == null) {
                                        data[0] = ZipFile.OpenRead(filename.ApartFromLast(@".json", StringComparison.OrdinalIgnoreCase) + @".zip");
                                        if (data[0] == null) return null;
                                    }

                                    return data[0].Entries.FirstOrDefault(x => string.Equals(x.FullName, s, StringComparison.OrdinalIgnoreCase))?
                                                  .Open()
                                                  .ReadAsBytesAndDispose();
                                });
                                if (i != null) {
                                    result.Add(i);
                                }
                            } catch (Exception e) {
                                Logging.Error(e);
                            }
                        }

                    }
                }

                return result;
            } finally {
                DisposeHelper.Dispose(ref data[0]);
            }
        }

        private static IEnumerable<PaintableItem> GetPaintableItems(string carId, [CanBeNull] Kn5 kn5, [NotNull] List<string> previousIds, 
                bool fallbackToGuess) {
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
                        return GetPaintableItems(j, kn5, previousIds, candidate);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            } else {
                foreach (var filename in FilesStorage.Instance.GetContentFilesFiltered(@"*.json", ContentCategory.PaintShop).Select(x => x.Filename)) {
                    try {
                        var j = JObject.Parse(File.ReadAllText(filename));
                        var d = j.GetValue(carId, StringComparison.OrdinalIgnoreCase) as JArray;
                        if (d != null) {
                            return GetPaintableItems(d, kn5, previousIds, filename);
                        }
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                }
            }

            return fallbackToGuess ? GuessPaintableItems(kn5) : new PaintableItem[0];
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

        public static IEnumerable<PaintableItem> GetPaintableItems(string carId, [CanBeNull] Kn5 kn5) {
            if (!PluginsManager.Instance.IsPluginEnabled(MagickPluginHelper.PluginId)) return new PaintableItem[0];

            var result = GetPaintableItems(carId, kn5, new List<string>(2), true).ToList();
            result.Sort(PaintableItemComparer.Instance);
            return result.Distinct(PaintableItemComparer.Instance);
        }
    }
}