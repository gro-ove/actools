// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Numerics {
    public partial struct Vec3 : IEquatable<Vec3> {
        public float this[int index] {
            get {
                switch (index) {
                    case 0:
                        return X;
                    case 1:
                        return Y;
                    case 2:
                        return Z;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vec3 run from 0 to 2, inclusive.");
                }
            }
            set {
                switch (index) {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                    case 2:
                        Z = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vec3 run from 0 to 2, inclusive.");
                }
            }
        }

        public static Vec3 Zero => new Vec3();
        public static Vec3 One => new Vec3(1f, 1f, 1f);
        public static Vec3 UnitX => new Vec3(1f, 0f, 0f);
        public static Vec3 UnitY => new Vec3(0f, 1f, 0f);
        public static Vec3 UnitZ => new Vec3(0f, 0f, 1f);

        public override int GetHashCode() {
            var hash = X.GetHashCode();
            hash = HashCodeHelper.CombineHashCodes(hash, Y.GetHashCode());
            hash = HashCodeHelper.CombineHashCodes(hash, Z.GetHashCode());
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) {
            return obj is Vec3 o && Equals(o);
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append(X.ToInvariantString());
            sb.Append(", ");
            sb.Append(Y.ToInvariantString());
            sb.Append(", ");
            sb.Append(Z.ToInvariantString());
            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining), Pure]
        public float Length() {
            var ls = X * X + Y * Y + Z * Z;
            return (float)Math.Sqrt(ls);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float LengthSquared() {
            return X * X + Y * Y + Z * Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(Vec3 value1, Vec3 value2) {
            var dx = value1.X - value2.X;
            var dy = value1.Y - value2.Y;
            var dz = value1.Z - value2.Z;
            var ls = dx * dx + dy * dy + dz * dz;
            return (float)Math.Sqrt(ls);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceSquared(Vec3 value1, Vec3 value2) {
            var dx = value1.X - value2.X;
            var dy = value1.Y - value2.Y;
            var dz = value1.Z - value2.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Normalize(Vec3 value) {
            var ls = value.X * value.X + value.Y * value.Y + value.Z * value.Z;
            var length = (float)Math.Sqrt(ls);
            return new Vec3(value.X / length, value.Y / length, value.Z / length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Cross(Vec3 vector1, Vec3 vector2) {
            return new Vec3(
                    vector1.Y * vector2.Z - vector1.Z * vector2.Y,
                    vector1.Z * vector2.X - vector1.X * vector2.Z,
                    vector1.X * vector2.Y - vector1.Y * vector2.X);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Reflect(Vec3 vector, Vec3 normal) {
            var dot = vector.X * normal.X + vector.Y * normal.Y + vector.Z * normal.Z;
            var tempX = normal.X * dot * 2f;
            var tempY = normal.Y * dot * 2f;
            var tempZ = normal.Z * dot * 2f;
            return new Vec3(vector.X - tempX, vector.Y - tempY, vector.Z - tempZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Clamp(Vec3 value1, Vec3 min, Vec3 max) {
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
            return new Vec3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Lerp(Vec3 value1, Vec3 value2, float amount) {
            return new Vec3(value1.X + (value2.X - value1.X) * amount, value1.Y + (value2.Y - value1.Y) * amount, value1.Z + (value2.Z - value1.Z) * amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Transform(Vec3 position, Mat4x4 matrix) {
            return new Vec3(
                    position.X * matrix.M11 + position.Y * matrix.M21 + position.Z * matrix.M31 + matrix.M41,
                    position.X * matrix.M12 + position.Y * matrix.M22 + position.Z * matrix.M32 + matrix.M42,
                    position.X * matrix.M13 + position.Y * matrix.M23 + position.Z * matrix.M33 + matrix.M43);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 TransformNormal(Vec3 normal, Mat4x4 matrix) {
            return new Vec3(
                    normal.X * matrix.M11 + normal.Y * matrix.M21 + normal.Z * matrix.M31,
                    normal.X * matrix.M12 + normal.Y * matrix.M22 + normal.Z * matrix.M32,
                    normal.X * matrix.M13 + normal.Y * matrix.M23 + normal.Z * matrix.M33);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Transform(Vec3 value, Quat rotation) {
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

            return new Vec3(
                    value.X * (1f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                    value.X * (xy2 + wz2) + value.Y * (1f - xx2 - zz2) + value.Z * (yz2 - wx2),
                    value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1f - xx2 - yy2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Add(Vec3 left, Vec3 right) {
            return left + right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Subtract(Vec3 left, Vec3 right) {
            return left - right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Multiply(Vec3 left, Vec3 right) {
            return left * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Multiply(Vec3 left, float right) {
            return left * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Multiply(float left, Vec3 right) {
            return left * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Divide(Vec3 left, Vec3 right) {
            return left / right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Divide(Vec3 left, float divisor) {
            return left / divisor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Negate(Vec3 value) {
            return -value;
        }
    }
}