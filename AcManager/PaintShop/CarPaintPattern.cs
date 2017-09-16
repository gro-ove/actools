using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Color = System.Windows.Media.Color;

namespace AcManager.PaintShop {
    public sealed class CarPaintPattern : Displayable {
        public static CarPaintPattern Nothing => new CarPaintPattern("Nothing", PaintShopSource.Transparent, null, null, new CarPaintColors(), null);

        public CarPaintPattern(string name, [NotNull] PaintShopSource source, [CanBeNull] PaintShopSource overlay, [CanBeNull] Size? size,
                CarPaintColors colors, [CanBeNull] IEnumerable<PaintShopPatternNumbers> numbers) {
            DisplayName = name;
            Source = source;
            Overlay = overlay;
            Colors = colors;
            Size = size;
            Numbers = numbers?.ToList() ?? new List<PaintShopPatternNumbers>(0);
            colors.PropertyChanged += OnColorsChanged;
        }

        public string LiveryStyle { get; set; }

        public Color[] ActualColors => Colors.ActualColors;

        private void OnColorsChanged(object sender, PropertyChangedEventArgs e) {
            OnPropertyChanged(nameof(ActualColors));
        }

        [NotNull]
        public PaintShopSource Source { get; }

        [CanBeNull]
        public PaintShopSource Overlay { get; }

        [NotNull]
        public CarPaintColors Colors { get; }

        [CanBeNull]
        public Size? Size { get; }

        [NotNull]
        public List<PaintShopPatternNumbers> Numbers { get; }

        public bool HasNumbers => Numbers.Count > 0;

        // which color is in which slot, −1 if there is no color in given slot
        [CanBeNull]
        public int[] LiveryColorIds { get; set; }

        [NotNull]
        public Dictionary<int, Color> LiveryColors => LiveryColorIds?.Select((x, i) => new {
            Slot = i,
            Color = x == -1 ? (Color?)null : ActualColors.ElementAtOrDefault(x)
        }).Where(x => x.Color.HasValue).ToDictionary(x => x.Slot, x => x.Color.Value) ?? ActualColors.ToDictionary((x, i) => i, (x, i) => x);
    }
}