using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public static partial class PaintShop {
        private static IEnumerable<PaintableItem> GuessPaintableItemsInner([CanBeNull] Kn5 kn5) {
            if (kn5 == null) yield break;

            var carPaint = new[] { "car_paint.dds", "Metal_detail.dds", "carpaint_detail.dds", "metal_detail.dds", "carpaint.dds" }
                    .FirstOrDefault(x => kn5.Textures.ContainsKey(x));
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

            var rims = new[] { "car_paint_rims.dds", "metal_detail_rim.dds", "Metal_detail_rim.dds" }
                    .Where(x => kn5.Textures.ContainsKey(x))
                    .Select(x => new ColoredItem(new PaintShopDestination(x), Colors.AliceBlue) { DisplayName = "Rims", Enabled = false })
                    .FirstOrDefault();
            if (rims != null) yield return rims;

            var calipers = new[] { "caliper_colour.dds", "metal_detail_caliper.dds", "Metal_detail_caliper.dds" }
                    .Where(x => kn5.Textures.ContainsKey(x))
                    .Select(x => new ColoredItem(new PaintShopDestination(x), Colors.DarkRed) { DisplayName = "Calipers", Enabled = false })
                    .FirstOrDefault();
            if (calipers != null) yield return calipers;

            var rollCage = new[] { "car_paint_roll_cage.dds" }
                    .Where(x => kn5.Textures.ContainsKey(x))
                    .Select(x => new ColoredItem(new PaintShopDestination(x), Colors.AliceBlue) { DisplayName = "Roll cage", Enabled = false })
                    .FirstOrDefault();
            if (rollCage != null) yield return rollCage;

            var glass = new[] { "ext_glass.dds" }
                    .Where(x => kn5.Textures.ContainsKey(x))
                    .Select(x => new TintedWindows(new PaintShopDestination(x)) { Enabled = false })
                    .FirstOrDefault();
            if (glass != null) yield return glass;
        }

        private static IEnumerable<PaintableItem> GuessPaintableItems([CanBeNull] Kn5 kn5) {
            foreach (var item in GuessPaintableItemsInner(kn5)) {
                item.Guessed = true;
                yield return item;
            }
        }
    }
}