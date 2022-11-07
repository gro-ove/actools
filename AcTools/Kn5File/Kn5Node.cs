using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using AcTools.Numerics;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public class Kn5Node {
        [NotNull]
        public string Name;

        public Kn5NodeClass NodeClass;
        public bool Active;

        /* Base */
        public Mat4x4 Transform;
        public List<Kn5Node> Children;

        /* Mesh */
        public bool CastShadows, IsVisible, IsTransparent, IsRenderable;
        public Bone[] Bones;
        public Vertex[] Vertices;
        public VerticeWeight[] VerticeWeights;
        public ushort[] Indices;
        public uint MaterialId, Layer;
        public float LodIn, LodOut;
        public Vec3 BoundingSphereCenter;
        public float BoundingSphereRadius;
        
        [CanBeNull]
        public Vec2[] Uv2;

        /* Only for skinned meshes */
        public byte[] MisteryBytes;

        /* For export, guaranteed to be unique */
        public string UniqueName;

        /* For local use if needed */
        public object Tag;

        [StructLayout(LayoutKind.Sequential)]
        public struct Vertex {
            public Vec3 Position, Normal, Tangent;
            public Vec2 Tex;

            public Vertex(Vec3 position, Vec3 normal, Vec2 tex, Vec3 tangent) {
                Position = position;
                Normal = normal;
                Tex = tex;
                Tangent = tangent;
            }

            public bool Equals(Vertex other) {
                return Position.Equals(other.Position) && Normal.Equals(other.Normal) && Tangent.Equals(other.Tangent) && Tex.Equals(other.Tex);
            }

            public override bool Equals(object obj) {
                return obj is Vertex other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = Position.GetHashCode();
                    hashCode = (hashCode * 397) ^ Normal.GetHashCode();
                    hashCode = (hashCode * 397) ^ Tangent.GetHashCode();
                    hashCode = (hashCode * 397) ^ Tex.GetHashCode();
                    return hashCode;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VerticeWeight {
            public Vec4 Weights;

            /// <summary>
            /// IDs of bones. "-1" if there is no binding!
            /// </summary>
            /// <remarks>Yes! Those are floats! There is no mistake here!</remarks>
            public Vec4 Indices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Bone {
            public string Name;
            public Mat4x4 Transform;
        }

        public Kn5Node() {
            Name = string.Empty;
        }

        public int TotalVerticesCount => NodeClass == Kn5NodeClass.Base ? Children.Sum(n => n.TotalVerticesCount) : Vertices.Length;

        public int TotalTrianglesCount => NodeClass == Kn5NodeClass.Base ? Children.Sum(n => n.TotalTrianglesCount) : Indices.Length / 3;

        public static Kn5Node CreateBaseNode(string name, List<Kn5Node> children = null, bool identityMatrix = false) {
            return new Kn5Node {
                NodeClass = Kn5NodeClass.Base,
                Name = name,
                Active = true,
                Children = children ?? new List<Kn5Node>(),
                Transform = Mat4x4.Identity
            };
        }

        [CanBeNull]
        public Kn5Node GetByName(string name) {
            return NodeClass == Kn5NodeClass.Base ? Children.FirstOrDefault(child => child.Name == name) : null;
        }
    }

    public enum Kn5NodeClass {
        [Description("Dummy")]
        Base = 1,

        [Description("Mesh")]
        Mesh = 2,

        [Description("Skinned mesh")]
        SkinnedMesh = 3
    }

    public static class Kn5NodeExtension {
        public static bool IsValidNodeClass(this int v) {
            return v >= 1 && v <= 3;
        }
    }
}