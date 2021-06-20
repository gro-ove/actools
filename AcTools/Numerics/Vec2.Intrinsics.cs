// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace AcTools.Numerics {
    public partial struct Vec2 {
        public float X;
        public float Y;

        public Vec2(float value) : this(value, value) { }

        public Vec2(float x, float y) {
            X = x;
            Y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(float[] array) {
            CopyTo(array, 0);
        }

        public void CopyTo(float[] array, int index) {
            array[index] = X;
            array[index + 1] = Y;
        }

        public bool Equals(Vec2 other) {
            return X == other.X && Y == other.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Vec2 a, Vec2 b) {
            return a.X * b.X + a.Y * b.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 Min(Vec2 a, Vec2 b) {
            return new Vec2(a.X < b.X ? a.X : b.X, a.Y < b.Y ? a.Y : b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 Max(Vec2 a, Vec2 b) {
            return new Vec2(a.X > b.X ? a.X : b.X, a.Y > b.Y ? a.Y : b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 Abs(Vec2 value) {
            return new Vec2(Math.Abs(value.X), Math.Abs(value.Y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 SquareRoot(Vec2 value) {
            return new Vec2((float)Math.Sqrt(value.X), (float)Math.Sqrt(value.Y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator +(Vec2 left, Vec2 right) {
            return new Vec2(left.X + right.X, left.Y + right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator -(Vec2 left, Vec2 right) {
            return new Vec2(left.X - right.X, left.Y - right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator *(Vec2 left, Vec2 right) {
            return new Vec2(left.X * right.X, left.Y * right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator *(float left, Vec2 right) {
            return new Vec2(left, left) * right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator *(Vec2 left, float right) {
            return left * new Vec2(right, right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator /(Vec2 left, Vec2 right) {
            return new Vec2(left.X / right.X, left.Y / right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator /(Vec2 value1, float value2) {
            var invDiv = 1f / value2;
            return new Vec2(value1.X * invDiv, value1.Y * invDiv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator -(Vec2 value) {
            return Zero - value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vec2 left, Vec2 right) {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vec2 left, Vec2 right) {
            return !(left == right);
        }
    }
}