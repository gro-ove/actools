using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopSourceParams {
        public bool Desaturate { get; set; }

        public bool NormalizeMax { get; set; }

        public bool RequiresPreparation => Desaturate || NormalizeMax;
    }

    public enum PaintShopSourceChannel {
        Red = 'r',
        Green = 'g',
        Blue = 'b',
        Alpha = 'a',
        Zero = '0',
        One = '1'
    }

    public class PaintShopSource : PaintShopSourceParams {
        public static PaintShopSource InputSource => new PaintShopSource();
        public static PaintShopSource White => new PaintShopSource(System.Drawing.Color.White);
        public static PaintShopSource Transparent => new PaintShopSource(System.Drawing.Color.Transparent);

        public readonly bool UseInput;

        public readonly Color? Color;

        [CanBeNull]
        public readonly string Name;

        [CanBeNull]
        public readonly byte[] Data;
        
        public PaintShopSourceChannel RedFrom { get; private set; } = PaintShopSourceChannel.Red;

        public PaintShopSourceChannel GreenFrom { get; private set; } = PaintShopSourceChannel.Green;

        public PaintShopSourceChannel BlueFrom { get; private set; } = PaintShopSourceChannel.Blue;

        public PaintShopSourceChannel AlphaFrom { get; private set; } = PaintShopSourceChannel.Alpha;

        public bool ByChannels => RedChannelSource != null || BlueChannelSource != null ||
                GreenChannelSource != null || AlphaChannelSource != null;

        public bool Custom => Desaturate || NormalizeMax ||
                RedFrom != PaintShopSourceChannel.Red || GreenFrom != PaintShopSourceChannel.Green ||
                BlueFrom != PaintShopSourceChannel.Blue || AlphaFrom != PaintShopSourceChannel.Alpha ||
                ByChannels;

        public PaintShopSource() {
            UseInput = true;
        }

        [NotNull]
        public PaintShopSource MapChannels([CanBeNull] string postfix) {
            if (!string.IsNullOrWhiteSpace(postfix)) {
                postfix = postfix.ToLowerInvariant();
                var last = postfix[postfix.Length - 1];
                RedFrom = (PaintShopSourceChannel)postfix.ElementAtOr(0, last);
                GreenFrom = (PaintShopSourceChannel)postfix.ElementAtOr(1, last);
                BlueFrom = (PaintShopSourceChannel)postfix.ElementAtOr(2, last);
                AlphaFrom = (PaintShopSourceChannel)postfix.ElementAtOr(3, (char)PaintShopSourceChannel.Alpha);
            }

            return this;
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

        [CanBeNull]
        public readonly PaintShopSource RedChannelSource, GreenChannelSource, BlueChannelSource, AlphaChannelSource;

        public PaintShopSource(PaintShopSource red, PaintShopSource green, PaintShopSource blue, PaintShopSource alpha) {
            RedChannelSource = red;
            GreenChannelSource = green;
            BlueChannelSource = blue;
            AlphaChannelSource = alpha;
        }

        public PaintShopSource SetFrom([CanBeNull] PaintShopSourceParams baseParams) {
            if (baseParams == null || !baseParams.RequiresPreparation) return this;
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
                hashCode = (hashCode * 397) ^ (RedChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (GreenChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (BlueChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (AlphaChannelSource?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ RedFrom.GetHashCode();
                hashCode = (hashCode * 397) ^ GreenFrom.GetHashCode();
                hashCode = (hashCode * 397) ^ BlueFrom.GetHashCode();
                hashCode = (hashCode * 397) ^ AlphaFrom.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() {
            if (UseInput) return "( PaintShopSource: use input )";
            if (Color != null) return $"( PaintShopSource: color={Color.Value} )";
            if (Name != null) return $"( PaintShopSource: name={Name} )";
            if (Data != null) return $"( PaintShopSource: {Data} bytes )";

            if (RedChannelSource != null || BlueChannelSource != null || RedChannelSource != null ||
                    AlphaChannelSource != null) {
                return $"( PaintShopSource: (R={RedChannelSource}, G={GreenChannelSource}, B={BlueChannelSource}, A={AlphaChannelSource}) )";
            }

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
                [NotNull] PaintShopSource source, [CanBeNull] PaintShopSource maskSource);

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
                [NotNull] PaintShopSource source, [CanBeNull] PaintShopSource maskSource);

        /// <summary>
        /// Several colors — for mask, in provided.
        /// </summary>
        Task SaveTextureTintAsync(string filename, [NotNull] Color[] colors, double alphaAdd, [NotNull] PaintShopSource source,
                [CanBeNull] PaintShopSource maskSource, [CanBeNull] PaintShopSource overlay);
    }
}