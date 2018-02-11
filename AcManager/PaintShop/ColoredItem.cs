using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.PaintShop {
    public class ColoredItem : AspectsPaintableItem {
        [NotNull]
        protected readonly Dictionary<PaintShopDestination, PaintShop.TintedEntry> Replacements;

        public ColoredItem([NotNull, Localizable(false)] PaintShopDestination diffuseTexture, Color defaultColor)
                : this(diffuseTexture, new CarPaintColors(defaultColor)) {}

        public ColoredItem([NotNull, Localizable(false)] PaintShopDestination diffuseTexture, [NotNull] CarPaintColors colors)
                : this(new Dictionary<PaintShopDestination, PaintShop.TintedEntry> {
                    [diffuseTexture] = new PaintShop.TintedEntry(PaintShopSource.White, null, null)
                }, colors) {}

        public ColoredItem([NotNull] Dictionary<PaintShopDestination, PaintShop.TintedEntry> replacements, [NotNull] CarPaintColors colors) : base(false) {
            Replacements = replacements;
            Colors = colors;
            Colors.PropertyChanged += OnColorsChanged;
        }

        protected override void Initialize() {
            foreach (var replacement in Replacements) {
                RegisterAspect(replacement.Key, GetOverride);
            }
        }

        private PaintShopOverrideBase GetOverride(PaintShopDestination name) {
            var replacement = Replacements.GetValueOrDefault(name);
            return new PaintShopOverrideTint {
                Colors = Colors.DrawingColors,
                Alpha = ValueAdjustment.Same,
                Source = replacement?.Source,
                Mask = replacement?.Mask,
                Overlay = replacement?.Overlay
            };
        }

        // which color is in which slot, −1 if there is no color in given slot
        [CanBeNull]
        public int[] LiveryColorIds { get; set; }

        public override Dictionary<int, Color> LiveryColors => LiveryColorIds?.Select((x, i) => new {
            Slot = i,
            Color = x == -1 ? (Color?)null : ActualColors.ArrayElementAtOrDefault(x)
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
            return Colors.ActualColors.ArrayElementAtOrDefault(colorIndex);
        }
    }
}