using System;
using SlimDX;

namespace AcTools.Render.Base.Utils {
    public static partial class MathF {
        public static readonly float Sqrt2 = (float) Math.Sqrt(2);
        

        private static readonly Random RandomObject = new Random();

        // ReSharper disable once InconsistentNaming
        public const float PI = (float)Math.PI;

        public static float Abs(float a) {
            return a < 0.0f ? -a : a;
        }

        public static float Sin(float rad) {
            return (float)Math.Sin(rad);
        }

        public static float Cos(float rad) {
            return (float)Math.Cos(rad);
        }

        public static float ToRadians(float degrees) {
            return PI * degrees / 180.0f;
        }

        public static float ToDegrees(float radians) {
            return radians * (180.0f / PI);
        }

        public static float Clamp(float value, float min, float max) {
            return Math.Max(min, Math.Min(value, max));
        }

        public static int Clamp(int value, int min, int max) {
            return Math.Max(min, Math.Min(value, max));
        }

        public static float Random() {
            return (float)RandomObject.NextDouble();
        }

        public static float Random(float min, float max) {
            return min + (float)RandomObject.NextDouble() * (max - min);
        }

        public static Matrix InverseTranspose(Matrix m) {
            var a = m;
            a.M41 = a.M42 = a.M43 = 0;
            a.M44 = 1;

            return Matrix.Transpose(Matrix.Invert(a));
        }

        public static float Tan(float a) {
            return (float)Math.Tan(a);
        }

        public static float Atan(float f) {
            return (float)Math.Atan(f);
        }


        // heightmap functions
        public static float Noise(int x) {
            x = (x << 13) ^ x;
            return (1.0f - ((x * (x * x * 15731) + 1376312589) & 0x7fffffff) / 1073741824.0f);
        }

        public static float CosInterpolate(float v1, float v2, float a) {
            var angle = a * PI;
            var prc = (1.0f - LookupCos(angle)) * 0.5f;
            return v1 * (1.0f - prc) + v2 * prc;
        }
        public static float PerlinNoise2D(int seed, float persistence, int octave, float x, float y) {
            var freq = (float)Math.Pow(2.0f, octave);
            var amp = (float)Math.Pow(persistence, octave);
            var tx = x * freq;
            var ty = y * freq;
            var txi = (int)tx;
            var tyi = (int)ty;
            var fracX = tx - txi;
            var fracY = ty - tyi;

            var v1 = Noise(txi + tyi * 57 + seed);
            var v2 = Noise(txi + 1 + tyi * 57 + seed);
            var v3 = Noise(txi + (tyi + 1) * 57 + seed);
            var v4 = Noise(txi + 1 + (tyi + 1) * 57 + seed);

            var i1 = CosInterpolate(v1, v2, fracX);
            var i2 = CosInterpolate(v3, v4, fracX);
            var f = CosInterpolate(i1, i2, fracY) * amp;
            return f;
        }

        public static float Sqrt(float f) {
            return (float)Math.Sqrt(f);
        }

        public static float Exp(float f) {
            return (float)Math.Exp(f);
        }

        public static float Pow(float x, float y) {
            return (float) Math.Pow(x, y);
        }

        public static Vector3 RandVector(Vector3 min, Vector3 max) {
            return new Vector3(Random(min.X, max.X), Random(min.Y, max.Y), Random(min.Z, max.Z));
        }

        public static int Random(int max) { return RandomObject.Next(max); }

        public static float AngleFromXY(float x, float y) {
            float theta;
            if (x >= 0.0f) {
                theta = Atan(y/x);
                if (theta < 0.0f) {
                    theta += 2*PI;
                }
            } else {
                theta = Atan(y/x) + PI;
            }
            return theta;
        }

        public static float Acos(float f) {
            return (float) Math.Acos(f);
        }
    }
}
