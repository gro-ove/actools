using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Kn5Render.Kn5Render {
    [StructLayout(LayoutKind.Sequential)]
    public struct ShaderMaterial {
        public Color4 Ambient, Diffuse, Specular, Fresnel;
        public float FresnelMax, MinAlpha, UseDetail, UseNormal, UseMap, DetailUVMultiplier;

        public static readonly int Stride = Marshal.SizeOf(typeof(ShaderMaterial));
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DirectionalLight {
        public Color4 Ambient, Diffuse, Specular;
        public Vector3 Direction;
        public float Pad;

        public static readonly int Stride = Marshal.SizeOf(typeof(DirectionalLight));
    }
        
    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once InconsistentNaming
    internal struct VerticePT {
        public readonly Vector3 Position;
        public readonly Vector2 Tex;

        public VerticePT(Vector3 p, Vector2 t) {
            Position = p;
            Tex = t;
        }

        public static readonly int Stride = Marshal.SizeOf(typeof(VerticePT));

        public static readonly InputElement[] InputElements = {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0)
        };
    }
        
    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once InconsistentNaming
    internal struct VerticePNT {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 Tex;

        public VerticePNT(Vector3 p, Vector3 n, Vector2 t) {
            Position = p;
            Normal = n;
            Tex = t;
        }

        public static readonly int Stride = Marshal.SizeOf(typeof(VerticePNT));

        public static readonly InputElement[] InputElements = {
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0)
        };
    }
}
