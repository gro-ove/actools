// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.Runtime.CompilerServices;
using SlimDX;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    public struct FbxVertex : IEquatable<FbxVertex> {
        public const uint SizeInBytes = 56;

        public Vector3 Position { get; set; }
        public Vector2 TexCoord { get; set; }
        public Vector3 Normal { get; set; }
        public Vector3 Tangent { get; set; }
        public Vector3 Binormal { get; set; }

        public FbxVertex(Vector3 position, Vector2 texCoord, Vector3 normal, Vector3 tangent, Vector3 binormal) {
            Position = position;
            TexCoord = texCoord;
            Normal = normal;
            Tangent = tangent;
            Binormal = binormal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) {
            if (!(obj is FbxVertex)) {
                return false;
            }
            return Equals((FbxVertex)obj);
        }

        public bool Equals(FbxVertex other) {
            return Position == other.Position && TexCoord == other.TexCoord && Normal == other.Normal && Tangent == other.Tangent && Binormal == other.Binormal;
        }

        private static int CombineHashCodes(int h1, int h2) {
            uint shift5 = ((uint)h1 << 5) | ((uint)h1 >>  27);
            return ((int)shift5 + h1) ^ h2;
        }

        public override int GetHashCode() {
            int hash = Position.GetHashCode();
            hash = CombineHashCodes(hash, TexCoord.GetHashCode());
            hash = CombineHashCodes(hash, Normal.GetHashCode());
            hash = CombineHashCodes(hash, Tangent.GetHashCode());
            hash = CombineHashCodes(hash, Binormal.GetHashCode());
            return hash;
        }

        public static bool operator ==(FbxVertex left, FbxVertex right) {
            return left.Equals(right);
        }

        public static bool operator !=(FbxVertex left, FbxVertex right) {
            return !(left == right);
        }
    }
}