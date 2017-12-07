using System;
using AcTools.Utils;
using SlimDX;

namespace AcTools.Render.Base.Utils {
    public static partial class MathF {
        /// <summary>
        /// Converts spherical coordinates (elevation and azimuthal angles) to the usual ones.
        /// </summary>
        /// <param name="θDeg">Elevation angle.</param>
        /// <param name="φDeg">Azimuthal angle.</param>
        /// <returns></returns>
        public static Vector3 ToVector3Deg(float θDeg, float φDeg) {
            return ToVector3Rad(θDeg.ToRadians(), φDeg.ToRadians());
        }

        /// <summary>
        /// Converts spherical coordinates (elevation and azimuthal angles) to the usual ones.
        /// </summary>
        /// <param name="θRad">Elevation angle.</param>
        /// <param name="φRad">Azimuthal angle.</param>
        /// <returns></returns>
        public static Vector3 ToVector3Rad(float θRad, float φRad) {
            var θ = PI / 2f - θRad;
            var sinθ = θ.Sin();
            var cosθ = θ.Cos();
            var sinφ = φRad.Sin();
            var cosφ = φRad.Cos();
            return new Vector3(sinθ * cosφ, cosθ, sinθ * sinφ);
        }

        private static readonly Random RandomObject = new Random();

        // ReSharper disable once InconsistentNaming
        public const float PI = (float)Math.PI;
        public const float ToRad = (float)Math.PI/180f;
        public const float ToDeg = 180f/(float)Math.PI;

        public static float ToRadians(this float degrees) {
            return ToRad * degrees;
        }

        public static float ToDegrees(this float radians) {
            return ToDeg * radians;
        }

        public static float Random() {
            return (float)RandomObject.NextDouble();
        }

        public static float Random(float min, float max) {
            return min + (float)RandomObject.NextDouble() * (max - min);
        }

        public static Matrix InverseTranspose(this Matrix m) {
            var a = m;
            a.M41 = a.M42 = a.M43 = 0;
            a.M44 = 1;

            return Matrix.Transpose(Matrix.Invert(a));
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

        public static float Sqrt(this float f) {
            return (float)Math.Sqrt(f);
        }

        public static float Exp(this float f) {
            return (float)Math.Exp(f);
        }

        public static float Pow(float x, float y) {
            return (float)Math.Pow(x, y);
        }

        public static Vector3 RandVector(Vector3 min, Vector3 max) {
            return new Vector3(Random(min.X, max.X), Random(min.Y, max.Y), Random(min.Z, max.Z));
        }

        public static int Random(int max) { return RandomObject.Next(max); }

        public static float AngleFromXY(float x, float y) {
            float theta;
            if (x >= 0.0f) {
                theta = (y / x).Atan();
                if (theta < 0.0f) {
                    theta += 2 * PI;
                }
            } else {
                theta = (y / x).Atan() + PI;
            }
            return theta;
        }
    }
}
