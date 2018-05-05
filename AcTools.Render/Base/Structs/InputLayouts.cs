using System.Runtime.InteropServices;
using AcTools.Kn5File;
using AcTools.Render.Base.Utils;
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
    public static class InputLayouts {
        public interface ILayout {
            int Stride { get; }

            InputElement[] InputElements { get; }
        }

        public interface IPositionLayout : ILayout {
            Vector3 Position { get; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticeP : IPositionLayout {
            public Vector3 Position;

            public VerticeP(Vector3 p) {
                Position = p;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticeP));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0)
            };

            public int Stride => StrideValue;

            public InputElement[] InputElements => InputElementsValue;

            Vector3 IPositionLayout.Position => Position;

            public static VerticeP[] Convert(Kn5Node.Vertex[] vertices) {
                var size = vertices.Length;
                var result = new VerticeP[size];

                for (var i = 0; i < size; i++) {
                    var x = vertices[i];
                    result[i].Position.X = x.Position[0];
                    result[i].Position.Y = x.Position[1];
                    result[i].Position.Z = x.Position[2];
                }

                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePT : IPositionLayout {
            public Vector3 Position;
            public Vector2 Tex;

            public VerticePT(Vector3 p, Vector2 t) {
                Position = p;
                Tex = t;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticePT));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0)
            };

            public int Stride => StrideValue;

            public InputElement[] InputElements => InputElementsValue;

            Vector3 IPositionLayout.Position => Position;

            public static VerticePT[] Convert(Kn5Node.Vertex[] vertices) {
                var size = vertices.Length;
                var result = new VerticePT[size];

                for (var i = 0; i < size; i++) {
                    var x = vertices[i];
                    result[i].Position.X = x.Position[0];
                    result[i].Position.Y = x.Position[1];
                    result[i].Position.Z = x.Position[2];
                    result[i].Tex.X = x.TexC[0];
                    result[i].Tex.Y = x.TexC[1];
                }

                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePC : IPositionLayout {
            public Vector3 Position;
            public Vector4 Color;

            public VerticePC(Vector3 p, Vector4 c) {
                Position = p;
                Color = c;
            }

            public VerticePC(Vector3 p, Color4 c) {
                Position = p;
                Color = (Vector4)c;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticePC));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0, InputClassification.PerVertexData, 0)
            };

            public int Stride => StrideValue;

            public InputElement[] InputElements => InputElementsValue;

            Vector3 IPositionLayout.Position => Position;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePNT : IPositionLayout {
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

            public int Stride => StrideValue;

            public InputElement[] InputElements => InputElementsValue;

            Vector3 IPositionLayout.Position => Position;

            public static VerticePNT[] Convert(Kn5Node.Vertex[] vertices) {
                var size = vertices.Length;
                var result = new VerticePNT[size];

                for (var i = 0; i < size; i++) {
                    var x = vertices[i];
                    result[i].Position.X = x.Position[0];
                    result[i].Position.Y = x.Position[1];
                    result[i].Position.Z = x.Position[2];
                    result[i].Normal.X = x.Normal[0];
                    result[i].Normal.Y = x.Normal[1];
                    result[i].Normal.Z = x.Normal[2];
                    result[i].Tex.X = x.TexC[0];
                    result[i].Tex.Y = x.TexC[1];
                }

                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePNTG : IPositionLayout {
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

            public int Stride => StrideValue;

            public InputElement[] InputElements => InputElementsValue;

            Vector3 IPositionLayout.Position => Position;

            public static VerticePNTG[] Convert(Kn5Node.Vertex[] vertices) {
                var size = vertices.Length;
                var result = new VerticePNTG[size];

                for (var i = 0; i < size; i++) {
                    var x = vertices[i];
                    result[i].Position.X = x.Position[0];
                    result[i].Position.Y = x.Position[1];
                    result[i].Position.Z = x.Position[2];
                    result[i].Normal.X = x.Normal[0];
                    result[i].Normal.Y = x.Normal[1];
                    result[i].Normal.Z = x.Normal[2];
                    result[i].Tex.X = x.TexC[0];
                    result[i].Tex.Y = x.TexC[1];
                    result[i].Tangent.X = x.TangentU[0];
                    result[i].Tangent.Y = x.TangentU[1];
                    result[i].Tangent.Z = x.TangentU[2];
                }

                return result;
            }

            public static VerticePNTG[] Convert(GeometryGenerator.Vertex[] vertices) {
                var size = vertices.Length;
                var result = new VerticePNTG[size];

                for (var i = 0; i < size; i++) {
                    var x = vertices[i];
                    result[i].Position.X = x.Position.X;
                    result[i].Position.Y = x.Position.Y;
                    result[i].Position.Z = x.Position.Z;
                    result[i].Normal.X = x.Normal.X;
                    result[i].Normal.Y = x.Normal.Y;
                    result[i].Normal.Z = x.Normal.Z;
                    result[i].Tex.X = x.TexC.X;
                    result[i].Tex.Y = x.TexC.Y;
                    result[i].Tangent.X = x.TangentU.X;
                    result[i].Tangent.Y = x.TangentU.Y;
                    result[i].Tangent.Z = x.TangentU.Z;
                }

                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticePNTGW4B : IPositionLayout {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Tex;
            public Vector3 Tangent;
            public Vector3 BonesWeights;
            public Vector4 BonesIndices;

            public VerticePNTGW4B(Vector3 p, Vector3 n, Vector2 t, Vector3 g, Vector3 bw, Vector4 bi) {
                Position = p;
                Normal = n;
                Tex = t;
                Tangent = g;
                BonesWeights = bw;
                BonesIndices = bi;
            }

            public static readonly int StrideValue = Marshal.SizeOf(typeof(VerticePNTGW4B));

            public static readonly InputElement[] InputElementsValue = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0),
                new InputElement("TANGENT", 0, Format.R32G32B32_Float, 32, 0, InputClassification.PerVertexData, 0),
                new InputElement("BLENDWEIGHTS", 0, Format.R32G32B32_Float, 44, 0, InputClassification.PerVertexData, 0),
                new InputElement("BLENDINDICES", 0, Format.R32G32B32A32_Float, 56, 0, InputClassification.PerVertexData, 0),
            };

            public int Stride => StrideValue;

            public InputElement[] InputElements => InputElementsValue;

            Vector3 IPositionLayout.Position => Position;

            public static VerticePNTGW4B[] Convert(Kn5Node.Vertex[] vertices, Kn5Node.VerticeWeight[] weights) {
                var size = vertices.Length;
                var result = new VerticePNTGW4B[size];

                for (var i = 0; i < size; i++) {
                    var x = vertices[i];
                    result[i].Position.X = x.Position[0];
                    result[i].Position.Y = x.Position[1];
                    result[i].Position.Z = x.Position[2];
                    result[i].Normal.X = x.Normal[0];
                    result[i].Normal.Y = x.Normal[1];
                    result[i].Normal.Z = x.Normal[2];
                    result[i].Tex.X = x.TexC[0];
                    result[i].Tex.Y = x.TexC[1];
                    result[i].Tangent.X = x.TangentU[0];
                    result[i].Tangent.Y = x.TangentU[1];
                    result[i].Tangent.Z = x.TangentU[2];

                    var w = weights[i];
                    result[i].BonesWeights.X = w.Weights[0];
                    result[i].BonesWeights.Y = w.Weights[1];
                    result[i].BonesWeights.Z = w.Weights[2];
                    result[i].BonesIndices.X = w.Indices[0] < 0 ? 0 : w.Indices[0];
                    result[i].BonesIndices.Y = w.Indices[1] < 0 ? 0 : w.Indices[1];
                    result[i].BonesIndices.Z = w.Indices[2] < 0 ? 0 : w.Indices[2];
                    result[i].BonesIndices.W = w.Indices[3] < 0 ? 0 : w.Indices[3];
                }

                return result;
            }
        }
    }
}
