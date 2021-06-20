// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace AcTools.Numerics {
    public partial struct Vec3 {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float value) : this(value, value, value) { }

        public Vec3(Vec2 value, float z) : this(value.X, value.Y, z) { }

        public Vec3(float x, float y, float z) {
            X = x;
            Y = y;
            Z = z;
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
        }

        public bool Equals(Vec3 other) {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Vec3 a, Vec3 b) {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 Min(Vec3 a, Vec3 b) {
            return new Vec3(a.X < b.X ? a.X : b.X, a.Y < b.Y ? a.Y : b.Y, a.Z < b.Z ? a.Z : b.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Max(Vec3 a, Vec3 b) {
            return new Vec3(a.X > b.X ? a.X : b.X, a.Y > b.Y ? a.Y : b.Y, a.Z > b.Z ? a.Z : b.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Abs(Vec3 value) {
            return new Vec3(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 SquareRoot(Vec3 value) {
            return new Vec3((float)Math.Sqrt(value.X), (float)Math.Sqrt(value.Y), (float)Math.Sqrt(value.Z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator +(Vec3 left, Vec3 right) {
            return new Vec3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator -(Vec3 left, Vec3 right) {
            return new Vec3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator *(Vec3 left, Vec3 right) {
            return new Vec3(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator *(Vec3 left, float right) {
            return left * new Vec3(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator *(float left, Vec3 right) {
            return new Vec3(left) * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator /(Vec3 left, Vec3 right) {
            return new Vec3(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator /(Vec3 value1, float value2) {
            var invDiv = 1f / value2;
            return new Vec3(value1.X * invDiv, value1.Y * invDiv, value1.Z * invDiv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator -(Vec3 value) {
            return Zero - value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vec3 left, Vec3 right) {
            return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vec3 left, Vec3 right) {
            return left.X != right.X || left.Y != right.Y || left.Z != right.Z;
        }
    }
}