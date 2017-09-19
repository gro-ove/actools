using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using AcTools.Render.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
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

        bool OverrideTexturePattern(string textureName, [CanBeNull] PaintShopSource ao, [NotNull] PaintShopSource pattern,
                [CanBeNull] PaintShopSource overlay, [CanBeNull] PaintShopSource underlay, [NotNull] Color[] colors,
                int? number, [NotNull] IReadOnlyList<PaintShopPatternNumber> numbers,
                [CanBeNull] string flagTexture, [NotNull] IReadOnlyList<PaintShopPatternFlag> flags,
                [CanBeNull] Size? forceSize);

        bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular, bool fixGloss,
                [NotNull] PaintShopSource source, [CanBeNull] PaintShopSource maskSource);

        /// <summary>
        /// Several colors — for mask, in provided.
        /// </summary>
        bool OverrideTextureTint(string textureName, [NotNull] Color[] colors, double alphaAdd, [NotNull] PaintShopSource source,
                [CanBeNull] PaintShopSource maskSource, [CanBeNull] PaintShopSource overlay);

        Task SaveTextureAsync(string filename, PreferredDdsFormat format, [NotNull] PaintShopSource source);

        Task SaveTextureAsync(string filename, PreferredDdsFormat format, Color color, double alpha);

        Task SaveTextureFlakesAsync(string filename, PreferredDdsFormat format, Color color, int size, double flakes);

        Task SaveTexturePatternAsync(string filename, PreferredDdsFormat format, [CanBeNull] PaintShopSource ao, [NotNull] PaintShopSource pattern,
                [CanBeNull] PaintShopSource overlay, [CanBeNull] PaintShopSource underlay, [NotNull] Color[] colors,
                int? number, [NotNull] IReadOnlyList<PaintShopPatternNumber> numbers,
                [CanBeNull] string flagTexture, [NotNull] IReadOnlyList<PaintShopPatternFlag> flags,
                [CanBeNull] Size? forceSize);

        Task SaveTextureMapsAsync(string filename, PreferredDdsFormat format, double reflection, double gloss, double specular, bool fixloss,
                [NotNull] PaintShopSource source, [CanBeNull] PaintShopSource maskSource);

        /// <summary>
        /// Several colors — for mask, in provided.
        /// </summary>
        Task SaveTextureTintAsync(string filename, PreferredDdsFormat format, [NotNull] Color[] colors, double alphaAdd, [NotNull] PaintShopSource source,
                [CanBeNull] PaintShopSource maskSource, [CanBeNull] PaintShopSource overlay);

        void SetCurrentSkinActive(bool active);
    }
}