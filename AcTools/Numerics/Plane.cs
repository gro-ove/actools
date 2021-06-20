// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AcTools.Numerics {
    public struct Plane : IEquatable<Plane> {
        public Vec3 Normal;
        public float D;

        public Plane(float x, float y, float z, float d) {
            Normal = new Vec3(x, y, z);
            D = d;
        }

        public Plane(Vec3 normal, float d) {
            Normal = normal;
            D = d;
        }

        public Plane(Vec4 value) {
            Normal = new Vec3(value.X, value.Y, value.Z);
            D = value.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane CreateFromVertices(Vec3 point1, Vec3 point2, Vec3 point3) {
            var ax = point2.X - point1.X;
            var ay = point2.Y - point1.Y;
            var az = point2.Z - point1.Z;

            var bx = point3.X - point1.X;
            var by = point3.Y - point1.Y;
            var bz = point3.Z - point1.Z;

            // N=Cross(a,b)
            var nx = ay * bz - az * @by;
            var ny = az * bx - ax * bz;
            var nz = ax * @by - ay * bx;

            // Normalize(N)
            var ls = nx * nx + ny * ny + nz * nz;
            var invNorm = 1.0f / (float)Math.Sqrt(ls);
            var normal = new Vec3(nx * invNorm, ny * invNorm, nz * invNorm);
            return new Plane(normal, -(normal.X * point1.X + normal.Y * point1.Y + normal.Z * point1.Z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane Normalize(Plane value) {
            const float FLT_EPSILON = 1.192092896e-07f; // smallest such that 1.0+FLT_EPSILON != 1.0
            var f = value.Normal.X * value.Normal.X + value.Normal.Y * value.Normal.Y + value.Normal.Z * value.Normal.Z;

            if (Math.Abs(f - 1.0f) < FLT_EPSILON) {
                return value; // It already normalized, so we don't need to further process.
            }

            var fInv = 1.0f / (float)Math.Sqrt(f);
            return new Plane(value.Normal.X * fInv, value.Normal.Y * fInv, value.Normal.Z * fInv, value.D * fInv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane Transform(Plane plane, Mat4x4 matrix) {
            Mat4x4.Invert(matrix, out var m);
            float x = plane.Normal.X, y = plane.Normal.Y, z = plane.Normal.Z, w = plane.D;
            return new Plane(
                    x * m.M11 + y * m.M12 + z * m.M13 + w * m.M14,
                    x * m.M21 + y * m.M22 + z * m.M23 + w * m.M24,
                    x * m.M31 + y * m.M32 + z * m.M33 + w * m.M34,
                    x * m.M41 + y * m.M42 + z * m.M43 + w * m.M44);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane Transform(Plane plane, Quat rotation) {
            // Compute rotation matrix.
            var x2 = rotation.X + rotation.X;
            var y2 = rotation.Y + rotation.Y;
            var z2 = rotation.Z + rotation.Z;

            var wx2 = rotation.W * x2;
            var wy2 = rotation.W * y2;
            var wz2 = rotation.W * z2;
            var xx2 = rotation.X * x2;
            var xy2 = rotation.X * y2;
            var xz2 = rotation.X * z2;
            var yy2 = rotation.Y * y2;
            var yz2 = rotation.Y * z2;
            var zz2 = rotation.Z * z2;

            var m11 = 1.0f - yy2 - zz2;
            var m21 = xy2 - wz2;
            var m31 = xz2 + wy2;

            var m12 = xy2 + wz2;
            var m22 = 1.0f - xx2 - zz2;
            var m32 = yz2 - wx2;

            var m13 = xz2 - wy2;
            var m23 = yz2 + wx2;
            var m33 = 1.0f - xx2 - yy2;

            float x = plane.Normal.X, y = plane.Normal.Y, z = plane.Normal.Z;
            return new Plane(x * m11 + y * m21 + z * m31, x * m12 + y * m22 + z * m32, x * m13 + y * m23 + z * m33, plane.D);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Plane plane, Vec4 value) {
            return plane.Normal.X * value.X + plane.Normal.Y * value.Y + plane.Normal.Z * value.Z + plane.D * value.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotCoordinate(Plane plane, Vec3 value) {
            return plane.Normal.X * value.X + plane.Normal.Y * value.Y + plane.Normal.Z * value.Z + plane.D;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotNormal(Plane plane, Vec3 value) {
            return plane.Normal.X * value.X + plane.Normal.Y * value.Y + plane.Normal.Z * value.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Plane value1, Plane value2) {
            return value1.Normal.X == value2.Normal.X && value1.Normal.Y == value2.Normal.Y && value1.Normal.Z == value2.Normal.Z && value1.D == value2.D;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Plane value1, Plane value2) {
            return value1.Normal.X != value2.Normal.X ||
                    value1.Normal.Y != value2.Normal.Y ||
                    value1.Normal.Z != value2.Normal.Z ||
                    value1.D != value2.D;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Plane other) {
            return Normal.X == other.Normal.X && Normal.Y == other.Normal.Y && Normal.Z == other.Normal.Z && D == other.D;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) {
            return obj is Plane o && Equals(o);
        }

        public override string ToString() {
            var ci = CultureInfo.CurrentCulture;
            return string.Format(ci, "{{Normal:{0} D:{1}}}", Normal.ToString(), D.ToString(ci));
        }

        public override int GetHashCode() {
            return Normal.GetHashCode() + D.GetHashCode();
        }
    }
}