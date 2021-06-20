// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace AcTools.Numerics {
    public partial struct Vec4 {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Vec4(float value)
                : this(value, value, value, value) { }

        public Vec4(float x, float y, float z, float w) {
            W = w;
            X = x;
            Y = y;
            Z = z;
        }

        public Vec4(Vec2 value, float z, float w) {
            X = value.X;
            Y = value.Y;
            Z = z;
            W = w;
        }

        public Vec4(Vec3 value, float w) {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
            W = w;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(float[] array) {
            CopyTo(array, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(float[] array, int index) {
            array[index] = X;
            array[index + 1] = Y;
            array[index + 2] = Z;
            array[index + 3] = W;
        }

        public bool Equals(Vec4 o) {
            return X == o.X && Y == o.Y && Z == o.Z && W == o.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Vec4 a, Vec4 b) {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Min(Vec4 a, Vec4 b) {
            return new Vec4(a.X < b.X ? a.X : b.X, a.Y < b.Y ? a.Y : b.Y, a.Z < b.Z ? a.Z : b.Z, a.W < b.W ? a.W : b.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Max(Vec4 a, Vec4 b) {
            return new Vec4(a.X > b.X ? a.X : b.X, a.Y > b.Y ? a.Y : b.Y, a.Z > b.Z ? a.Z : b.Z, a.W > b.W ? a.W : b.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 Abs(Vec4 value) {
            return new Vec4(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z), Math.Abs(value.W));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 SquareRoot(Vec4 value) {
            return new Vec4((float)Math.Sqrt(value.X), (float)Math.Sqrt(value.Y), (float)Math.Sqrt(value.Z), (float)Math.Sqrt(value.W));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator +(Vec4 left, Vec4 right) {
            return new Vec4(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator -(Vec4 left, Vec4 right) {
            return new Vec4(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator *(Vec4 left, Vec4 right) {
            return new Vec4(left.X * right.X, left.Y * right.Y, left.Z * right.Z, left.W * right.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator *(Vec4 left, float right) {
            return left * new Vec4(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator *(float left, Vec4 right) {
            return new Vec4(left) * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator /(Vec4 left, Vec4 right) {
            return new Vec4(left.X / right.X, left.Y / right.Y, left.Z / right.Z, left.W / right.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator /(Vec4 value1, float value2) {
            var invDiv = 1.0f / value2;
            return new Vec4(value1.X * invDiv, value1.Y * invDiv, value1.Z * invDiv, value1.W * invDiv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec4 operator -(Vec4 value) {
            return Zero - value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vec4 left, Vec4 right) {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vec4 left, Vec4 right) {
            return !(left == right);
        }
    }
}