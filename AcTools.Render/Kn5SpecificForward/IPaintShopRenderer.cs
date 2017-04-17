using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopSourceParams {
        public bool Desaturate { get; set; }

        public bool NormalizeMax { get; set; }
    }

    public class PaintShopSource : PaintShopSourceParams {
        public static readonly PaintShopSource InputSource = new PaintShopSource();
        public static readonly PaintShopSource White = new PaintShopSource(System.Drawing.Color.White);
        public static readonly PaintShopSource Transparent = new PaintShopSource(System.Drawing.Color.Transparent);

        public readonly bool UseInput;

        public readonly Color? Color;

        [CanBeNull]
        public readonly string Name;

        [CanBeNull]
        public readonly byte[] Data;

        public PaintShopSource() {
            UseInput = true;
        }

        public PaintShopSource(Color baseColor) {
            Color = baseColor;
        }

        public PaintShopSource([NotNull] string name) {
            Name = name;
        }

        public PaintShopSource([NotNull] byte[] data) {
            Data = data;
        }

        private PaintShopSource(bool useInput, Color? color, string name, byte[] data) {
            UseInput = useInput;
            Color = color;
            Name = name;
            Data = data;
        }

        public PaintShopSource CopyFrom([CanBeNull] PaintShopSourceParams baseParams) {
            return baseParams == null ? this : new PaintShopSource(UseInput, Color, Name, Data).SetFrom(baseParams);
        }

        public PaintShopSource SetFrom([CanBeNull] PaintShopSourceParams baseParams) {
            if (baseParams == null) return this;
            foreach (var p in typeof(PaintShopSourceParams).GetProperties().Where(p => p.CanWrite)) {
                p.SetValue(this, p.GetValue(baseParams, null), null);
            }
            return this;
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = UseInput ? -1 : Color?.GetHashCode() ?? Name?.GetHashCode() ?? Data?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ Desaturate.GetHashCode();
                hashCode = (hashCode * 397) ^ NormalizeMax.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() {
            if (UseInput) return "( PaintShopSource: use input )";
            if (Color != null) return $"( PaintShopSource: color={Color.Value} )";
            if (Name != null) return $"( PaintShopSource: name={Name} )";
            if (Data != null) return $"( PaintShopSource: {Data} bytes )";
            return "( PaintShopSource: nothing )";
        }
    }

    public interface IPaintShopRenderer {
        /// <summary>
        /// Override texture.
        /// </summary>
        /// <param name="textureName">Name of a texture being replaced.</param>
        /// <param name="source">If null, reset to its original state.</param>
        /// <returns>True if successfull</returns>
        bool OverrideTexture(string textureName, [CanBeNull] PaintShopSource source);

        bool OverrideTexture(string textureName, Color color, double alpha);

        bool OverrideTextureFlakes(string textureName, Color color, int size, double flakes);

        bool OverrideTexturePattern(string textureName, [NotNull] PaintShopSource ao, [NotNull] PaintShopSource pattern,
                [CanBeNull] PaintShopSource overlay, [NotNull] Color[] colors);

        bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular, bool fixGloss,
                [NotNull] PaintShopSource source);

        /// <summary>
        /// Several colors — for mask, in provided.
        /// </summary>
        bool OverrideTextureTint(string textureName, [NotNull] Color[] colors, double alphaAdd, [NotNull] PaintShopSource source,
                [CanBeNull] PaintShopSource maskSource, [CanBeNull] PaintShopSource overlay);

        Task SaveTextureAsync(string filename, [NotNull] PaintShopSource source);

        Task SaveTextureAsync(string filename, Color color, double alpha);

        Task SaveTextureFlakesAsync(string filename, Color color, int size, double flakes);

        Task SaveTexturePatternAsync(string filename, [NotNull] PaintShopSource ao, [NotNull] PaintShopSource pattern,
                [CanBeNull] PaintShopSource overlay, [NotNull] Color[] colors);

        Task SaveTextureMapsAsync(string filename, double reflection, double gloss, double specular, bool fixloss,
                [NotNull] PaintShopSource source);

        /// <summary>
        /// Several colors — for mask, in provided.
        /// </summary>
        Task SaveTextureTintAsync(string filename, [NotNull] Color[] colors, double alphaAdd, [NotNull] PaintShopSource source,
                [CanBeNull] PaintShopSource maskSource, [CanBeNull] PaintShopSource overlay);
    }
}