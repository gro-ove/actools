using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace AcTools.Kn5File {
    public class Kn5Node {
        public string Name;
        public Kn5NodeClass NodeClass;
        public bool Active;

        /* Base */
        public float[] Transform;
        public List<Kn5Node> Children;

        /* Mesh */
        public bool CastShadows, IsVisible, IsTransparent, IsRenderable;
        public Bone[] Bones;
        public Vertice[] Vertices;
        public VerticeWeight[] VerticeWeights;
        public ushort[] Indices;
        public uint MaterialId, Layer;
        public float LodIn, LodOut;
        public float[] BoundingSphereCenter;
        public float BoundingSphereRadius;

        [StructLayout(LayoutKind.Sequential)]
        public struct Vertice {
            public float[] Co, Normal, Uv, Tangent;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticeWeight {
            public float[] Weights;

            /// <summary>
            /// IDs of bones. "-1" if there is no binding!
            /// </summary>
            public float[] Indices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Bone {
            public string Name;
            public float[] Transform;
        }

        internal Kn5Node() { }

        public int TotalVerticesCount {
            get {
                return NodeClass == Kn5NodeClass.Base ? Children.Sum(kn5Node => kn5Node.TotalVerticesCount) : 
                    Vertices.Length;
            }
        }

        public int TotalTrianglesCount {
            get {
                return NodeClass == Kn5NodeClass.Base ? Children.Sum(kn5Node => kn5Node.TotalTrianglesCount) : 
                    (Indices.Length/3);
            }
        }

        public static Kn5Node CreateBaseNode(string name) {
            return new Kn5Node {
                NodeClass = Kn5NodeClass.Base,
                Name = name,
                Active = true,
                Children = new List<Kn5Node>(),
                Transform = new [] {
                    1.0f, 0.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 0.0f, 0.0f,
                    0.0f, 0.0f, 1.0f, 0.0f,
                    1.0f, 0.0f, 0.0f, 0.0f 
                }
            };
        }

        public Kn5Node GetByName(string name) {
            return NodeClass == Kn5NodeClass.Base ? Children.FirstOrDefault(child => child.Name == name) : null;
        }
    }

    public enum Kn5NodeClass {
        [Description("Dummy")]
        Base = 1,

        [Description("Mesh")]
        Mesh = 2,

        [Description("Skinned Mesh")]
        SkinnedMesh = 3
    }
}
