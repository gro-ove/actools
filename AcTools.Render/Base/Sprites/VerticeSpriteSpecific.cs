using System.Runtime.InteropServices;
using AcTools.Render.Base.Structs;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.Sprites {
    [StructLayout(LayoutKind.Sequential)]
    public struct VerticeSpriteSpecific : InputLayouts.ILayout {
        internal Vector2 TexCoord;
        internal Vector2 TexCoordSize;
        internal int Color;
        internal Vector2 TopLeft;
        internal Vector2 TopRight;
        internal Vector2 BottomLeft;
        internal Vector2 BottomRight;

        public VerticeSpriteSpecific(Vector2 texCoord, Vector2 texCoordSize, int color, Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft,
                Vector2 bottomRight) {
            TexCoord = texCoord;
            TexCoordSize = texCoordSize;
            Color = color;
            TopLeft = topLeft;
            TopRight = topRight;
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;
        }

        public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticeSpriteSpecific));

        public static readonly InputElement[] InputElementsValue = {
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElement("TEXCOORDSIZE", 0, Format.R32G32_Float, 8, 0, InputClassification.PerVertexData, 0),
            new InputElement("COLOR", 0, Format.B8G8R8A8_UNorm, 16, 0, InputClassification.PerVertexData, 0),
            new InputElement("TOPLEFT", 0, Format.R32G32_Float, 20, 0, InputClassification.PerVertexData, 0),
            new InputElement("TOPRIGHT", 0, Format.R32G32_Float, 28, 0, InputClassification.PerVertexData, 0),
            new InputElement("BOTTOMLEFT", 0, Format.R32G32_Float, 36, 0, InputClassification.PerVertexData, 0),
            new InputElement("BOTTOMRIGHT", 0, Format.R32G32_Float, 44, 0, InputClassification.PerVertexData, 0)
        };

        public int Stride => StrideValue;

        public InputElement[] InputElements => InputElementsValue;
    }
}