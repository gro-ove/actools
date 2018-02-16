using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Color = System.Windows.Media.Color;

namespace AcManager.PaintShop {
    public class CarPaintPatternDecals : NotifyPropertyChanged {
        public PaintShopPatternDecal Decal { get; }
        public Dictionary<string, PaintShopSource> Candidates { get; }

        public CarPaintPatternDecals(PaintShopPatternDecal decal, Dictionary<string, PaintShopSource> candidates) {
            Decal = decal;
            Candidates = candidates;
            Selected = candidates.FirstOrDefault();
        }

        private KeyValuePair<string, PaintShopSource> _selected;

        public KeyValuePair<string, PaintShopSource> Selected {
            get => _selected;
            set => Apply(value, ref _selected);
        }
    }

    public sealed class CarPaintPattern : Displayable {
        public static CarPaintPattern Nothing => new CarPaintPattern("Nothing", PaintShopSource.Transparent,
                null, null, null, null, null, new CarPaintColors(), null, null, null, null);

        public CarPaintPattern(string name, [CanBeNull] PaintShopSource source,
                [CanBeNull] PaintShopSource overlay, [CanBeNull] PaintShopSource underlay,
                bool? overlayWithoutAo, bool? underlayWithoutAo,
                [CanBeNull] Size? size, CarPaintColors colors,
                [CanBeNull] IEnumerable<PaintShopPatternNumber> numbers,
                [CanBeNull] IEnumerable<PaintShopPatternFlag> flags,
                [CanBeNull] IEnumerable<PaintShopPatternLabel> labels,
                [CanBeNull] IEnumerable<Tuple<PaintShopPatternDecal, Dictionary<string, PaintShopSource>>> decals) {
            DisplayName = name;
            Source = source;
            Overlay = overlay;
            Underlay = underlay;
            OverlayWithoutAo = overlayWithoutAo;
            UnderlayWithoutAo = underlayWithoutAo;
            Colors = colors;
            Size = size;
            Numbers = numbers?.NonNull().ToList() ?? new List<PaintShopPatternNumber>(0);
            Flags = flags?.NonNull().ToList() ?? new List<PaintShopPatternFlag>(0);
            Labels = labels?.NonNull().ToList() ?? new List<PaintShopPatternLabel>(0);
            DecalsWithCandidates = decals?.NonNull().Select(x => new CarPaintPatternDecals(x.Item1, x.Item2)).ToList() ?? new List<CarPaintPatternDecals>(0);
            Decals = DecalsWithCandidates.Select(x => x.Decal).ToList();

            Logging.Debug($"{name}: {Colors.Colors.Length} colors, {Numbers.Count} numbers, {Flags.Count} flags, {Labels.Count} labels, {Decals.Count} decals");

            colors.PropertyChanged += OnColorsChanged;

            foreach (var decal in DecalsWithCandidates) {
                decal.PropertyChanged += OnDecalPropertyChanged;
            }

            foreach (var colorRef in Numbers.Concat(Labels).Select(x => x.ColorRef)
                                            .Concat(Decals.Select(x => x.ColorRef)).Where(x => x.IsReference)) {
                colorRef.Updated += OnLabelColorRefUpdated;
            }
        }

        /// <summary>
        /// Only for OnPropertyChanged(nameof(LabelColors)) bit.
        /// </summary>
        public IEnumerable<System.Drawing.Color> LabelColors => Numbers.Concat(Labels).Select(x => x.ColorRef)
                                                                       .Concat(Decals.Select(x => x.ColorRef)).Select(x => x.GetValue(null));

        /// <summary>
        /// Only for OnPropertyChanged(nameof(DecalSources)) bit.
        /// </summary>
        public IEnumerable<PaintShopSource> DecalSources => Decals.Select(x => x.Source);

        private void OnDecalPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(CarPaintPatternDecals.Selected)) {
                if (sender is CarPaintPatternDecals d) {
                    d.Decal.Source = d.Selected.Value;
                }

                OnPropertyChanged(nameof(DecalSources));
            }
        }

        private void OnLabelColorRefUpdated(object sender, EventArgs e) {
            OnPropertyChanged(nameof(LabelColors));
        }

        public string LiveryStyle { get; set; }

        public Color[] ActualColors => Colors.ActualColors;

        private void OnColorsChanged(object sender, PropertyChangedEventArgs e) {
            OnPropertyChanged(nameof(ActualColors));
        }

        [CanBeNull]
        public PaintShopSource Source { get; }

        [CanBeNull]
        public PaintShopSource Overlay { get; }

        [CanBeNull]
        public PaintShopSource Underlay { get; }

        public bool? OverlayWithoutAo { get; }
        public bool? UnderlayWithoutAo { get; }

        [NotNull]
        public CarPaintColors Colors { get; }

        [CanBeNull]
        public Size? Size { get; }

        [NotNull]
        public List<PaintShopPatternNumber> Numbers { get; }

        [NotNull]
        public List<PaintShopPatternFlag> Flags { get; }

        [NotNull]
        public List<PaintShopPatternLabel> Labels { get; }

        [NotNull]
        public List<PaintShopPatternDecal> Decals { get; }

        [NotNull]
        public List<CarPaintPatternDecals> DecalsWithCandidates { get; }

        public bool HasNumbers => Numbers.Count > 0;
        public bool HasFlags => Flags.Count > 0;

        private List<string> _labels;
        public IReadOnlyList<string> LabelRoles => _labels ?? (_labels = Labels.Select(x => x?.Role).Distinct().ToList());

        // which color is in which slot, −1 if there is no color in given slot
        [CanBeNull]
        public int[] LiveryColorIds { get; set; }

        [NotNull]
        public Dictionary<int, Color> LiveryColors => LiveryColorIds?.Select((x, i) => new {
            Slot = i,
            Color = x == -1 ? (Color?)null : ActualColors.ArrayElementAtOrDefault(x)
        }).Where(x => x.Color.HasValue).ToDictionary(x => x.Slot, x => x.Color.Value) ?? ActualColors.ToDictionary((x, i) => i, (x, i) => x);
    }
}