using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace AcManager.PaintShop {
    public static partial class PaintShop {
        [CanBeNull, Localizable(false)]
        private static string FindRegexTexture([NotNull] IKn5 kn5, [RegexPattern] string query) {
            return kn5.Textures.Keys.FirstOrDefault(new Regex(query, RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace).IsMatch);
        }

        [CanBeNull, Localizable(false)]
        private static PaintableItem FindRegex([NotNull] IKn5 kn5, Func<string, PaintableItem> prepare, [RegexPattern] string query) {
            var texture = FindRegexTexture(kn5, query);
            return texture != null ? prepare(texture) : null;
        }

        [CanBeNull, Localizable(false)]
        private static PaintableItem FindQuery([NotNull] IKn5 kn5, Func<string, PaintableItem> prepare, params string[] query) {
            var texture = query.Select(x => RegexFromQuery.IsQuery(x)
                    ? kn5.Textures.Keys.FirstOrDefault(RegexFromQuery.Create(x, StringMatchMode.CompleteMatch, false).IsMatch)
                    : kn5.Textures.Keys.Contains(x) ? x : null).FirstOrDefault(x => x != null);
            return texture != null ? prepare(texture) : null;
        }

        private static Func<string, ColoredItem> ItemTint(Color defaultColor, string displayName) {
            ColoredItem Fn(string x) {
                return new ColoredItem(new Dictionary<PaintShopDestination, TintedEntry> {
                    [new PaintShopDestination(x)] = new TintedEntry(new PaintShopSource(x).SetFrom(new PaintShopSourceParams {
                        DesaturateMax = true,
                        NormalizeMax = true,
                        /*RedAdjustment = new ValueAdjustment(0.137, 1d),
                        GreenAdjustment = new ValueAdjustment(0.137, 1d),
                        BlueAdjustment = new ValueAdjustment(0.137, 1d)*/
                    }), null, null)
                }, new CarPaintColors(defaultColor)) { DisplayName = displayName, Enabled = false };
            }

            return Fn;
        }

        private static Func<string, ColoredItem> ItemFill(Color defaultColor, string displayName) {
            ColoredItem Fn(string x) {
                return new ColoredItem(new PaintShopDestination(x), defaultColor) { DisplayName = displayName, Enabled = false };
            }

            return Fn;
        }

        private static Func<string, ColoredItem> ItemWindows() {
            ColoredItem Fn(string x) {
                return new TintedWindows(new PaintShopDestination(x)) { Enabled = false };
            }

            return Fn;
        }

        private static IEnumerable<PaintableItem> GuessPaintableItemsInner([CanBeNull] IKn5 kn5) {
            if (kn5 == null) yield break;

            var carPaint = FindRegexTexture(kn5, @"^(?:car_?paint|[mM]etal_[dD]etail_?1?|carpaint_detail|.*exterior_body_detail)?\.dds$");
            var mapsMap = kn5.Materials.Values.Where(x => x.ShaderName == "ksPerPixelMultiMap_damage_dirt")
                             .Select(x => x.GetMappingByName(@"txMaps")?.Texture)
                             .NonNull()
                             .FirstOrDefault();

            if (kn5.Textures.ContainsKey("Plate_D.dds") && kn5.Textures.ContainsKey("Plate_NM.dds")) {
                yield return new LicensePlate(LicensePlate.LicenseFormat.Europe);
            }

            if (carPaint != null) {
                yield return mapsMap == null ?
                        new CarPaint { LiveryStyle = "Flat" }
                                .SetDetailsParams(new PaintShopDestination(carPaint, PreferredDdsFormat.NoCompressionTransparency)) :
                        new ComplexCarPaint(
                                new PaintShopDestination(mapsMap), new PaintShopSource {
                                    NormalizeMax = true
                                }, null) { LiveryStyle = "Flat" }
                                .SetDetailsParams(new PaintShopDestination(carPaint, PreferredDdsFormat.NoCompressionTransparency));
            }

            yield return FindQuery(kn5, ItemFill(Colors.White, "Rims"),
                    "car_paint_rims.dds", "metal_detail_rim.dds", "Metal_detail_rim.dds", "rim_detail.dds", "rim_Color.dds");
            yield return FindQuery(kn5, ItemFill(Colors.DarkRed, "Calipers"),
                    "caliper_colour.dds", "metal_detail_caliper.dds", "Metal_detail_caliper.dds", "caliper_detail.dds", "EXT_Caliper_color.dds");
            yield return FindQuery(kn5, ItemFill(Colors.Red, "Cylinder head cover"),
                    "noise_D.dds");
            yield return FindQuery(kn5, ItemFill(Colors.White, "Roll cage"),
                    "car_paint_roll_cage.dds");
            yield return FindQuery(kn5, ItemFill(Colors.White, "Roof"),
                    "metal_detail_roof.dds");

            foreach (var i in Enumerable.Range(2, 6)) {
                yield return FindQuery(kn5, ItemFill(Colors.White, $"Car paint #{i}"),
                        $"metal_detail_{i}.dds", $"metal_detail_{(char)('A' + i - 1)}.dds");
                yield return FindQuery(kn5, ItemFill(Colors.White, $"Car skin #{i}"),
                        $"metal_detail_skin_{i}.dds", $"metal_detail_skin_{(char)('A' + i - 1)}.dds");
                yield return FindQuery(kn5, ItemFill(Colors.White, $"Rims #{i}"),
                        $"rim_detail_{i}.dds");
            }

            yield return FindQuery(kn5, ItemTint(Colors.White, "Carpet"),
                    "carpet.dds", "Carpet.dds", "Carpet.png");
            yield return FindRegex(kn5, ItemTint(Colors.Red, "Stitches"),
                    @"^(?:(?:\w+_)?[cC]uciture\.dds|(?:[sS]titches|seams_?D?)\.png)$");
            yield return FindQuery(kn5, ItemTint(Colors.Red, "Stitches logo"),
                    "seat_logo_D.dds");
            yield return FindQuery(kn5, ItemFill(Colors.White, "Plastic"),
                    "PlasticDetail_color.dds");
            yield return FindQuery(kn5, ItemTint(Colors.White, "Cloth"),
                    "TEssuto.dds", "cloth_detail.dds", "Velvet.dds");
            yield return FindQuery(kn5, ItemFill(Colors.White, "Cloth (piece)"),
                    "TEssuto_color.dds");
            yield return FindRegex(kn5, ItemTint(Colors.White, "Seats"),
                    @"^(?:alcnt_seat|[cC]loth_seats|canvas_D
                        |Fabric_Seat_Color|(?:\w+_)?Quadrettoni_COLOR|INT_Skin_Color
                        |.*interior_seat_color|.*(?:INT_)?[sS]eat(?:ing)?_details?)\.dds$");

            yield return FindQuery(kn5, ItemTint(Colors.White, "Kevlar"),
                    "kevlar_tile.dds");
            yield return FindQuery(kn5, ItemTint(Colors.White, "Interior"),
                    "alcnt.dds");
            yield return FindRegex(kn5, ItemTint(Colors.White, "Interior (piece)"),
                    @"^(?:alcnt_BLU|.*interior_detail)\.dds$");
            yield return FindRegex(kn5, ItemTint(Colors.White, "Leather"),
                    @"^(?:leather(?:_Roadster)?|INT_leather\d*)\.dds$");
            yield return FindRegex(kn5, ItemTint(Colors.White, "Leather (colored)"),
                    @"^(?:(?:INT_)?[Ll]eather\d*_(?![nN])\w+)\.dds$");

            foreach (var i in Enumerable.Range(0, 6)) {
                yield return FindRegex(kn5, ItemTint(Colors.White, $"Interior plastic #{i}"),
                        $@"^(?:.*interior_plastics_detail0?{i})\.dds$");
                yield return FindQuery(kn5, ItemTint(Colors.White, $"Carpet #{i}"),
                        $"carpet{i}.dds");
                yield return FindQuery(kn5, ItemTint(Colors.White, $"Trimming #{i}"),
                        $"INT_Trim{i}.dds");
                yield return FindQuery(kn5, ItemTint(Colors.White, $"Fabric #{i}"),
                        $"INT_fabric{i}.dds");
                yield return FindQuery(kn5, ItemTint(Colors.White, $"Seat #{i}"),
                        $"leather_seat{i}.dds");
                yield return FindQuery(kn5, ItemTint(Colors.White, $"Interior #{i}"),
                        $"alcnt_cust{i}.dds");
                yield return FindQuery(kn5, ItemTint(Colors.White, $"Leather #{i}"),
                        $"leather{i}.dds", $"leather_{i}.dds", $"alcnt_cust{i}.dds", $"leather_cust{i}.dds");
            }

            yield return FindQuery(kn5, ItemWindows(),
                    "ext_glass.dds");
        }

        private static IEnumerable<PaintableItem> GuessPaintableItems([CanBeNull] IKn5 kn5) {
            foreach (var item in GuessPaintableItemsInner(kn5).NonNull()) {
                item.Guessed = true;
                yield return item;
            }
        }
    }
}