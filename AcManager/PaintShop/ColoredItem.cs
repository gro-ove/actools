using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class ColoredItem : AspectsPaintableItem {
        [NotNull]
        protected readonly Dictionary<TextureFileName, PaintShop.TintedEntry> Replacements;

        public ColoredItem([NotNull, Localizable(false)] TextureFileName diffuseTexture, Color defaultColor)
                : this(diffuseTexture, new CarPaintColors(defaultColor)) {}

        public ColoredItem([NotNull, Localizable(false)] TextureFileName diffuseTexture, [NotNull] CarPaintColors colors)
                : this(new Dictionary<TextureFileName, PaintShop.TintedEntry> {
                    [diffuseTexture] = new PaintShop.TintedEntry(PaintShopSource.White, null, null)
                }, colors) {}

        public ColoredItem([NotNull] Dictionary<TextureFileName, PaintShop.TintedEntry> replacements, [NotNull] CarPaintColors colors) : base(false) {
            Replacements = replacements;
            Colors = colors;
            Colors.PropertyChanged += OnColorsChanged;
        }

        protected override void Initialize() {
            foreach (var replacement in Replacements) {
                RegisterAspect(replacement.Key, GetApply(replacement.Value), GetSave(replacement.Value));
            }
        }

        private Action<TextureFileName, IPaintShopRenderer> GetApply(PaintShop.TintedEntry v) {
            return (name, renderer) => renderer.OverrideTextureTint(name.FileName, Colors.DrawingColors, 0d, v.Source, v.Mask, v.Overlay);
        }

        private Func<string, TextureFileName, IPaintShopRenderer, Task> GetSave(PaintShop.TintedEntry v) {
            return (location, name, renderer) => renderer.SaveTextureTintAsync(Path.Combine(location, name.FileName), name.PreferredFormat,
                    Colors.DrawingColors, 0d, v.Source, v.Mask, v.Overlay);
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
            SetAllDirty();
            OnPropertyChanged(nameof(ActualColors));
            RaiseColorChanged(null);
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

        public override Color? GetColor(int colorIndex) {
            return Colors.ActualColors.ElementAtOrDefault(colorIndex);
        }
    }
}