using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

// ReSharper disable InconsistentNaming

namespace AcTools.Render.Base.Structs {
    /// <summary>
    /// P (v3) - position
    /// T (v2) - texture coordinate
    /// G (v3) - tangent (whatever it is)
    /// C (v4) - color
    /// N (v3) - normal
    /// </summary>
    public class InputLayouts {
        public interface ILayout {
            int Stride { get; }

            InputElement[] InputElements { get; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePT : ILayout {
            public readonly Vector3 Position;
            public readonly Vector2 Tex;

            public VerticePT(Vector3 p, Vector2 t) {
                Position = p;
                Tex = t;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticePT));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0)
            };

            public int Stride {
                get { return StrideValue; }
            }

            public InputElement[] InputElements {
                get { return InputElementsValue; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePC : ILayout {
            public Vector3 Position;
            public Vector4 Color;

            public VerticePC(Vector3 p, Vector4 c) {
                Position = p;
                Color = c;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticePC));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0, InputClassification.PerVertexData, 0)
            };

            public int Stride {
                get { return StrideValue; }
            }

            public InputElement[] InputElements {
                get { return InputElementsValue; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePNT : ILayout {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Tex;

            public VerticePNT(Vector3 p, Vector3 n, Vector2 t) {
                Position = p;
                Normal = n;
                Tex = t;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticePNT));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0)
            };

            public int Stride {
                get { return StrideValue; }
            }

            public InputElement[] InputElements {
                get { return InputElementsValue; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePNTG : ILayout {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Tex;
            public Vector3 Tangent;

            public VerticePNTG(Vector3 p, Vector3 n, Vector2 t, Vector3 g) {
                Position = p;
                Normal = n;
                Tex = t;
                Tangent = g;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticePNTG));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0),
                new InputElement("TANGENT", 0, Format.R32G32B32_Float, 32, 0, InputClassification.PerVertexData, 0)
            };

            public int Stride {
                get { return StrideValue; }
            }

            public InputElement[] InputElements {
                get { return InputElementsValue; }
            }
        }
    }
}
