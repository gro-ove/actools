using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class ColoredItem : PaintableItem {
        protected readonly Dictionary<TextureFileName, PaintShop.TintedEntry> Replacements;

        public ColoredItem([Localizable(false)] TextureFileName diffuseTexture, Color defaultColor)
                : this(diffuseTexture, new CarPaintColors(defaultColor)) {}

        public ColoredItem([Localizable(false)] TextureFileName diffuseTexture, CarPaintColors colors)
                : this(new Dictionary<TextureFileName, PaintShop.TintedEntry> {
                    [diffuseTexture] = new PaintShop.TintedEntry(PaintShopSource.White, null, null)
                }, colors) {}

        public ColoredItem(Dictionary<TextureFileName, PaintShop.TintedEntry> replacements, CarPaintColors colors) : base(false) {
            Replacements = replacements;
            Colors = colors;
            Colors.PropertyChanged += OnColorsChanged;
            AffectedTextures.AddRange(Replacements.Keys.Select(x => x.FileName));
        }

        // which color is in which slot, −1 if there is no color in given slot
        [CanBeNull]
        public int[] LiveryColorIds { get; set; }

        public override Dictionary<int, Color> LiveryColors => LiveryColorIds?.Select((x, i) => new {
            Slot = i,
            Color = x == -1 ? (Color?)null : ActualColors.ElementAtOrDefault(x)
        }).Where(x => x.Color.HasValue).ToDictionary(x => x.Slot, x => x.Color.Value) ?? base.LiveryColors;

        public override string DisplayName { get; set; } = "Colored item";

        public CarPaintColors Colors { get; }

        public Color[] ActualColors => Colors.ActualColors;

        private void OnColorsChanged(object sender, PropertyChangedEventArgs e) {
            OnPropertyChanged(nameof(ActualColors));
        }

        protected override void ApplyOverride(IPaintShopRenderer renderer) {
            foreach (var replacement in Replacements) {
                renderer.OverrideTextureTint(replacement.Key.FileName, Colors.DrawingColors, 0d,
                        replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
            }
        }

        protected override void ResetOverride(IPaintShopRenderer renderer) {
            foreach (var replacement in Replacements) {
                renderer.OverrideTexture(replacement.Key.FileName, null);
            }
        }

        protected override async Task SaveOverrideAsync(IPaintShopRenderer renderer, string location, CancellationToken cancellation) {
            foreach (var replacement in Replacements) {
                await renderer.SaveTextureTintAsync(Path.Combine(location, replacement.Key.FileName), replacement.Key.PreferredFormat,
                        Colors.DrawingColors, 0d, replacement.Value.Source, replacement.Value.Mask, replacement.Value.Overlay);
                if (cancellation.IsCancellationRequested) return;
            }
        }

        public override JObject Serialize() {
            var result = base.Serialize();
            if (result != null) {
                result["colors"] = SerializeColors(Colors);
            }

            return result;
        }

        public override void Deserialize(JObject data) {
            base.Deserialize(data);
            if (data != null) {
                DeserializeColors(Colors, data, "colors");
            }
        }
    }
}