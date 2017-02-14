using System;
using System.Collections.Generic;
using SlimDX;

namespace AcTools.Render.Base.Sprites {
    /// <summary>
    /// This structure holds data for sprites with a specific texture
    /// </summary>
    internal class SpriteSegment {
        /// <summary>
        /// The ShaderResourceView
        /// </summary>
        internal object Texture;

        internal readonly List<VerticeSpriteSpecific> Sprites = new List<VerticeSpriteSpecific>();
    }

    internal class CharTableDescription {
        /// <summary>
        /// A Texture2D
        /// </summary>
        internal IDisposable Texture = null;

        internal IDisposable Srv;
        internal readonly CharDescription[] Chars = new CharDescription[256];
    }

    internal class CharDescription {
        /// <summary>
        /// Size of the char excluding overhangs
        /// </summary>
        internal Vector2 CharSize;

        internal float OverhangLeft, OverhangRight, OverhangTop, OverhangBottom;

        internal Vector2 TexCoordsStart;
        internal Vector2 TexCoordsSize;

        internal CharTableDescription TableDescription;

        internal StringMetrics ToStringMetrics(Vector2 position, float scalX, float scalY) {
            return new StringMetrics {
                TopLeft = position,
                Size = new Vector2(CharSize.X * scalX, CharSize.Y * scalY),
                OverhangTop = Math.Abs(scalY) * OverhangTop,
                OverhangBottom = Math.Abs(scalY) * OverhangBottom,
                OverhangLeft = Math.Abs(scalX) * OverhangLeft,
                OverhangRight = Math.Abs(scalX) * OverhangRight,
            };
        }
    }
}
