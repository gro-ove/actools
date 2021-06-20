// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using AcTools.Utils.Helpers;

namespace AcTools.Numerics {
    public partial struct Vec4 : IEquatable<Vec4> {
        public static Vec4 Zero => new Vec4();
        public static Vec4 One => new Vec4(1f, 1f, 1f, 1f);
        public static Vec4 UnitX => new Vec4(1f, 0f, 0f, 0f);
        public static Vec4 UnitY => new Vec4(0f, 1f, 0f, 0f);
        public static Vec4 UnitZ => new Vec4(0f, 0f, 1f, 0f);
        public static Vec4 UnitW => new Vec4(0f, 0f, 0f, 1f);

        public override int GetHashCode() {
            var hash = X.GetHashCode();
            hash = HashCodeHelper.CombineHashCodes(hash, Y.GetHashCode());
            hash = HashCodeHelper.CombineHashCodes(hash, Z.GetHashCode());
            hash = HashCodeHelper.CombineHashCodes(hash, W.GetHashCode());
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) {
            return obj is Vec4 o && Equals(o);
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append(X.ToInvariantString());
            sb.Append(", ");
            sb.Append(Y.ToInvariantString());
            sb.Append(", ");
            sb.Append(Z.ToInvariantString());
            sb.Append(", ");
            sb.Append(W.ToInvariantString());
            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Length() {
            var ls = X * X + Y * Y + Z * Z + W * W;
            return (float)Math.Sqrt(ls);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float LengthSquared() {
            return X * X + Y * Y + Z * Z + W * W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(Vec4 value1, Vec4 value2) {
            var dx = value1.X - value2.X;
            var dy = value1.Y - value2.Y;
            var dz = value1.Z - value2.Z;
            var dw = value1.W - value2.W;

            var ls = dx * dx + dy * dy + dz * dz + dw * dw;

            return (float)Math.Sqrt(ls);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceSquared(Vec4 value1, Vec4 value2) {
            var dx = value1.X - value2.X;
            var dy = value1.Y - value2.Y;
            var dz = value1.Z - value2.Z;
            var dw = value1.W - value2.W;
            return dx * dx + dy * dy + dz * dz + dw * dw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Normalize(Vec4 vector) {
            var ls = vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z + vector.W * vector.W;
            var invNorm = 1f / (float)Math.Sqrt(ls);
            return new Vec4(vector.X * invNorm, vector.Y * invNorm, vector.Z * invNorm, vector.W * invNorm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Clamp(Vec4 value1, Vec4 min, Vec4 max) {
            // This compare order is very important!!!
            // We must follow HLSL behavior in the case user specified min value is bigger than max value.

            var x = value1.X;
            x = x > max.X ? max.X : x;
            x = x < min.X ? min.X : x;

            var y = value1.Y;
            y = y > max.Y ? max.Y : y;
            y = y < min.Y ? min.Y : y;

            var z = value1.Z;
            z = z > max.Z ? max.Z : z;
            z = z < min.Z ? min.Z : z;

            var w = value1.W;
            w = w > max.W ? max.W : w;
            w = w < min.W ? min.W : w;

            return new Vec4(x, y, z, w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Lerp(Vec4 value1, Vec4 value2, float amount) {
            return new Vec4(
                    value1.X + (value2.X - value1.X) * amount,
                    value1.Y + (value2.Y - value1.Y) * amount,
                    value1.Z + (value2.Z - value1.Z) * amount,
                    value1.W + (value2.W - value1.W) * amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Transform(Vec2 position, Mat4x4 matrix) {
            return new Vec4(
                    position.X * matrix.M11 + position.Y * matrix.M21 + matrix.M41,
                    position.X * matrix.M12 + position.Y * matrix.M22 + matrix.M42,
                    position.X * matrix.M13 + position.Y * matrix.M23 + matrix.M43,
                    position.X * matrix.M14 + position.Y * matrix.M24 + matrix.M44);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Transform(Vec3 position, Mat4x4 matrix) {
            return new Vec4(
                    position.X * matrix.M11 + position.Y * matrix.M21 + position.Z * matrix.M31 + matrix.M41,
                    position.X * matrix.M12 + position.Y * matrix.M22 + position.Z * matrix.M32 + matrix.M42,
                    position.X * matrix.M13 + position.Y * matrix.M23 + position.Z * matrix.M33 + matrix.M43,
                    position.X * matrix.M14 + position.Y * matrix.M24 + position.Z * matrix.M34 + matrix.M44);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Transform(Vec4 vector, Mat4x4 matrix) {
            return new Vec4(
                    vector.X * matrix.M11 + vector.Y * matrix.M21 + vector.Z * matrix.M31 + vector.W * matrix.M41,
                    vector.X * matrix.M12 + vector.Y * matrix.M22 + vector.Z * matrix.M32 + vector.W * matrix.M42,
                    vector.X * matrix.M13 + vector.Y * matrix.M23 + vector.Z * matrix.M33 + vector.W * matrix.M43,
                    vector.X * matrix.M14 + vector.Y * matrix.M24 + vector.Z * matrix.M34 + vector.W * matrix.M44);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Transform(Vec2 value, Quat rotation) {
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

            return new Vec4(
                    value.X * (1f - yy2 - zz2) + value.Y * (xy2 - wz2),
                    value.X * (xy2 + wz2) + value.Y * (1f - xx2 - zz2),
                    value.X * (xz2 - wy2) + value.Y * (yz2 + wx2),
                    1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Transform(Vec3 value, Quat rotation) {
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

            return new Vec4(
                    value.X * (1f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                    value.X * (xy2 + wz2) + value.Y * (1f - xx2 - zz2) + value.Z * (yz2 - wx2),
                    value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1f - xx2 - yy2),
                    1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Transform(Vec4 value, Quat rotation) {
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

            return new Vec4(
                    value.X * (1f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                    value.X * (xy2 + wz2) + value.Y * (1f - xx2 - zz2) + value.Z * (yz2 - wx2),
                    value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1f - xx2 - yy2),
                    value.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Add(Vec4 left, Vec4 right) {
            return left + right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Subtract(Vec4 left, Vec4 right) {
            return left - right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Multiply(Vec4 left, Vec4 right) {
            return left * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Multiply(Vec4 left, float right) {
            return left * new Vec4(right, right, right, right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Multiply(float left, Vec4 right) {
            return new Vec4(left, left, left, left) * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Divide(Vec4 left, Vec4 right) {
            return left / right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Divide(Vec4 left, float divisor) {
            return left / divisor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Negate(Vec4 value) {
            return -value;
        }
    }
}