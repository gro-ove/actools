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
        private static PaintableItem GetPaintableItem([NotNull] JObject e, Func<string, byte[]> extraData) {
            const string typeColor = "color";
            const string typeCarPaint = "carpaint";
            const string typeTintedWindow = "tintedwindow";
            const string typeLicensePlate = "licenseplate";
            const string typeSolidColorIfFlagged = "solidcolorifflagged";
            const string typeTransparentIfFlagged = "transparentifflagged";
            const string typeReplacedIfFlagged = "replacedifflagged";

            const string keyName = "name";
            const string keyEnabled = "enabled";
            const string keyInverse = "inverse";
            const string keyType = "type";
            const string keyStyle = "style";
            const string keyTexture = "texture";
            const string keyNormalsTexture = "normals";
            const string keyMapsDefault = "mapsTexture";
            const string keyMapsDefaultTexture = "mapsDefaultTexture";
            const string keyMapsAutoLevel = "mapsAutoLevel";
            const string keyColor = "color";
            const string keyOpacity = "opacity";
            const string keyDefaultColor = "defaultColor";
            const string keyDefaultOpacity = "defaultOpacity";
            const string keyTintBase = "tintBase";

            Func<JToken, PaintShopSource> getSource = j => {
                if (j.Type == JTokenType.Boolean) {
                    var b = (bool)j;
                    return b ? PaintShopSource.InputSource : null;
                }

                if (j.Type != JTokenType.String) {
                    return null;
                }

                var s = j.ToString();
                if (s.StartsWith("./") || s.StartsWith(".\\")) {
                    return new PaintShopSource(extraData(s.Substring(2)));
                } else {
                    return new PaintShopSource(s);
                }
            };

            PaintableItem result;

            var type = GetString(e, keyType, typeColor).ToLowerInvariant();
            switch (type) {
                case typeCarPaint:
                    var maps = GetString(e, keyMapsDefault);
                    if (maps != null) {
                        result = new ComplexCarPaint(RequireString(e, keyTexture), maps, GetColor(e, keyDefaultColor)) {
                            AutoAdjustLevels = e.GetBoolValueOnly(keyMapsAutoLevel) ?? false,
                            MapsDefaultTexture = getSource(e[keyMapsDefaultTexture]),
                        };
                    } else {
                        result = new CarPaint(RequireString(e, keyTexture), GetColor(e, keyDefaultColor));
                    }
                    break;
                case typeColor:
                    result = new ColoredItem(RequireString(e, keyTexture), GetColor(e, keyDefaultColor));
                    break;
                case typeLicensePlate:
                    result = new LicensePlate(GetString(e, keyStyle, "Europe"), GetString(e, keyTexture, "Plate_D.dds"),
                            GetString(e, keyNormalsTexture, "Plate_NM.dds"));
                    break;
                case typeTintedWindow:
                    result = new TintedWindows(RequireString(e, keyTexture), GetDouble(e, keyDefaultOpacity, 0.23),
                            GetColor(e, keyDefaultColor, Color.FromRgb(41, 52, 55)), e.GetBoolValueOnly(keyTintBase) ?? false);
                    break;
                case typeSolidColorIfFlagged:
                    result = new SolidColorIfFlagged(RequireString(e, keyTexture), e.GetBoolValueOnly(keyInverse) ?? false, 
                            GetColor(e, keyColor), GetDouble(e, keyOpacity, 0.23));
                    break;
                case typeTransparentIfFlagged:
                    result = new TransparentIfFlagged(RequireString(e, keyTexture), e.GetBoolValueOnly(keyInverse) ?? false);
                    break;
                case typeReplacedIfFlagged:
                    result = new ReplacedIfFlagged(e.GetBoolValueOnly(keyInverse) ?? false,
                            e["pairs"].ToObject<Dictionary<string, JToken>>().Select(x => new {
                                x.Key,
                                Source = getSource(x.Value)
                            }).Where(x => x.Source != null).ToDictionary(
                                    x => x.Key,
                                    x => x.Source));
                    break;
                default:
                    throw new Exception($"Not supported type: {type}");
            }

            var name = GetString(e, keyName);
            if (name != null) {
                result.DisplayName = name;
            }

            var enabled = e.GetBoolValueOnly(keyEnabled);
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
                                Logging.Error(e.Message);
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
                    Logging.Error(e.Message);
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
                        Logging.Error(e.Message);
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