// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace AcTools.Numerics {
    public struct Quat : IEquatable<Quat> {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public static Quat Identity => new Quat(0, 0, 0, 1);

        public bool IsIdentity => X == 0f && Y == 0f && Z == 0f && W == 1f;

        public Quat(float x, float y, float z, float w) {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Quat(Vec3 vectorPart, float scalarPart) {
            X = vectorPart.X;
            Y = vectorPart.Y;
            Z = vectorPart.Z;
            W = scalarPart;
        }

        public float Length() {
            var ls = X * X + Y * Y + Z * Z + W * W;
            return (float)Math.Sqrt(ls);
        }

        public float LengthSquared() {
            return X * X + Y * Y + Z * Z + W * W;
        }

        public static Quat Normalize(Quat value) {
            Quat ans;
            var ls = value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W;
            var invNorm = 1f / (float)Math.Sqrt(ls);
            ans.X = value.X * invNorm;
            ans.Y = value.Y * invNorm;
            ans.Z = value.Z * invNorm;
            ans.W = value.W * invNorm;
            return ans;
        }

        public static Quat Conjugate(Quat value) {
            Quat ans;
            ans.X = -value.X;
            ans.Y = -value.Y;
            ans.Z = -value.Z;
            ans.W = value.W;
            return ans;
        }

        public static Quat Inverse(Quat value) {
            //  -1   (       a              -v       )
            // q   = ( -------------   ------------- )
            //       (  a^2 + |v|^2  ,  a^2 + |v|^2  )

            Quat ans;
            var ls = value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W;
            var invNorm = 1f / ls;
            ans.X = -value.X * invNorm;
            ans.Y = -value.Y * invNorm;
            ans.Z = -value.Z * invNorm;
            ans.W = value.W * invNorm;
            return ans;
        }

        public static Quat CreateFromAxisAngle(Vec3 axis, float angle) {
            Quat ans;
            var halfAngle = angle * 0.5f;
            var s = (float)Math.Sin(halfAngle);
            var c = (float)Math.Cos(halfAngle);
            ans.X = axis.X * s;
            ans.Y = axis.Y * s;
            ans.Z = axis.Z * s;
            ans.W = c;
            return ans;
        }

        public static Quat CreateFromYawPitchRoll(float yaw, float pitch, float roll) {
            //  Roll first, about axis the object is facing, then
            //  pitch upward, then yaw to face into the new heading
            float sr, cr, sp, cp, sy, cy;

            var halfRoll = roll * 0.5f;
            sr = (float)Math.Sin(halfRoll);
            cr = (float)Math.Cos(halfRoll);

            var halfPitch = pitch * 0.5f;
            sp = (float)Math.Sin(halfPitch);
            cp = (float)Math.Cos(halfPitch);

            var halfYaw = yaw * 0.5f;
            sy = (float)Math.Sin(halfYaw);
            cy = (float)Math.Cos(halfYaw);

            Quat result;
            result.X = cy * sp * cr + sy * cp * sr;
            result.Y = sy * cp * cr - cy * sp * sr;
            result.Z = cy * cp * sr - sy * sp * cr;
            result.W = cy * cp * cr + sy * sp * sr;
            return result;
        }

        public static Quat CreateFromRotationMatrix(Mat4x4 matrix) {
            var trace = matrix.M11 + matrix.M22 + matrix.M33;

            var q = new Quat();

            if (trace > 0f) {
                var s = (float)Math.Sqrt(trace + 1f);
                q.W = s * 0.5f;
                s = 0.5f / s;
                q.X = (matrix.M23 - matrix.M32) * s;
                q.Y = (matrix.M31 - matrix.M13) * s;
                q.Z = (matrix.M12 - matrix.M21) * s;
            } else {
                if (matrix.M11 >= matrix.M22 && matrix.M11 >= matrix.M33) {
                    var s = (float)Math.Sqrt(1f + matrix.M11 - matrix.M22 - matrix.M33);
                    var invS = 0.5f / s;
                    q.X = 0.5f * s;
                    q.Y = (matrix.M12 + matrix.M21) * invS;
                    q.Z = (matrix.M13 + matrix.M31) * invS;
                    q.W = (matrix.M23 - matrix.M32) * invS;
                } else if (matrix.M22 > matrix.M33) {
                    var s = (float)Math.Sqrt(1f + matrix.M22 - matrix.M11 - matrix.M33);
                    var invS = 0.5f / s;
                    q.X = (matrix.M21 + matrix.M12) * invS;
                    q.Y = 0.5f * s;
                    q.Z = (matrix.M32 + matrix.M23) * invS;
                    q.W = (matrix.M31 - matrix.M13) * invS;
                } else {
                    var s = (float)Math.Sqrt(1f + matrix.M33 - matrix.M11 - matrix.M22);
                    var invS = 0.5f / s;
                    q.X = (matrix.M31 + matrix.M13) * invS;
                    q.Y = (matrix.M32 + matrix.M23) * invS;
                    q.Z = 0.5f * s;
                    q.W = (matrix.M12 - matrix.M21) * invS;
                }
            }

            return q;
        }

        public static float Dot(Quat quaternion1, Quat quaternion2) {
            return quaternion1.X * quaternion2.X +
                    quaternion1.Y * quaternion2.Y +
                    quaternion1.Z * quaternion2.Z +
                    quaternion1.W * quaternion2.W;
        }

        public static Quat Slerp(Quat quaternion1, Quat quaternion2, float amount) {
            const float epsilon = 1e-6f;

            var t = amount;

            var cosOmega = quaternion1.X * quaternion2.X + quaternion1.Y * quaternion2.Y +
                    quaternion1.Z * quaternion2.Z + quaternion1.W * quaternion2.W;

            var flip = false;

            if (cosOmega < 0f) {
                flip = true;
                cosOmega = -cosOmega;
            }

            float s1, s2;

            if (cosOmega > 1f - epsilon) {
                // Too close, do straight linear interpolation.
                s1 = 1f - t;
                s2 = flip ? -t : t;
            } else {
                var omega = (float)Math.Acos(cosOmega);
                var invSinOmega = (float)(1 / Math.Sin(omega));

                s1 = (float)Math.Sin((1f - t) * omega) * invSinOmega;
                s2 = flip
                        ? (float)-Math.Sin(t * omega) * invSinOmega
                        : (float)Math.Sin(t * omega) * invSinOmega;
            }

            Quat ans;

            ans.X = s1 * quaternion1.X + s2 * quaternion2.X;
            ans.Y = s1 * quaternion1.Y + s2 * quaternion2.Y;
            ans.Z = s1 * quaternion1.Z + s2 * quaternion2.Z;
            ans.W = s1 * quaternion1.W + s2 * quaternion2.W;

            return ans;
        }

        public static Quat Lerp(Quat quaternion1, Quat quaternion2, float amount) {
            var t = amount;
            var t1 = 1f - t;

            var r = new Quat();

            var dot = quaternion1.X * quaternion2.X + quaternion1.Y * quaternion2.Y +
                    quaternion1.Z * quaternion2.Z + quaternion1.W * quaternion2.W;

            if (dot >= 0f) {
                r.X = t1 * quaternion1.X + t * quaternion2.X;
                r.Y = t1 * quaternion1.Y + t * quaternion2.Y;
                r.Z = t1 * quaternion1.Z + t * quaternion2.Z;
                r.W = t1 * quaternion1.W + t * quaternion2.W;
            } else {
                r.X = t1 * quaternion1.X - t * quaternion2.X;
                r.Y = t1 * quaternion1.Y - t * quaternion2.Y;
                r.Z = t1 * quaternion1.Z - t * quaternion2.Z;
                r.W = t1 * quaternion1.W - t * quaternion2.W;
            }

            // Normalize it.
            var ls = r.X * r.X + r.Y * r.Y + r.Z * r.Z + r.W * r.W;
            var invNorm = 1f / (float)Math.Sqrt(ls);

            r.X *= invNorm;
            r.Y *= invNorm;
            r.Z *= invNorm;
            r.W *= invNorm;

            return r;
        }

        public static Quat Concatenate(Quat value1, Quat value2) {
            Quat ans;

            // Concatenate rotation is actually q2 * q1 instead of q1 * q2.
            // So that's why value2 goes q1 and value1 goes q2.
            var q1x = value2.X;
            var q1y = value2.Y;
            var q1z = value2.Z;
            var q1w = value2.W;

            var q2x = value1.X;
            var q2y = value1.Y;
            var q2z = value1.Z;
            var q2w = value1.W;

            // cross(av, bv)
            var cx = q1y * q2z - q1z * q2y;
            var cy = q1z * q2x - q1x * q2z;
            var cz = q1x * q2y - q1y * q2x;

            var dot = q1x * q2x + q1y * q2y + q1z * q2z;

            ans.X = q1x * q2w + q2x * q1w + cx;
            ans.Y = q1y * q2w + q2y * q1w + cy;
            ans.Z = q1z * q2w + q2z * q1w + cz;
            ans.W = q1w * q2w - dot;

            return ans;
        }

        public static Quat Negate(Quat value) {
            Quat ans;
            ans.X = -value.X;
            ans.Y = -value.Y;
            ans.Z = -value.Z;
            ans.W = -value.W;
            return ans;
        }

        public static Quat Add(Quat value1, Quat value2) {
            Quat ans;
            ans.X = value1.X + value2.X;
            ans.Y = value1.Y + value2.Y;
            ans.Z = value1.Z + value2.Z;
            ans.W = value1.W + value2.W;
            return ans;
        }

        public static Quat Subtract(Quat value1, Quat value2) {
            Quat ans;
            ans.X = value1.X - value2.X;
            ans.Y = value1.Y - value2.Y;
            ans.Z = value1.Z - value2.Z;
            ans.W = value1.W - value2.W;
            return ans;
        }

        public static Quat Multiply(Quat value1, Quat value2) {
            Quat ans;

            var q1x = value1.X;
            var q1y = value1.Y;
            var q1z = value1.Z;
            var q1w = value1.W;

            var q2x = value2.X;
            var q2y = value2.Y;
            var q2z = value2.Z;
            var q2w = value2.W;

            // cross(av, bv)
            var cx = q1y * q2z - q1z * q2y;
            var cy = q1z * q2x - q1x * q2z;
            var cz = q1x * q2y - q1y * q2x;

            var dot = q1x * q2x + q1y * q2y + q1z * q2z;

            ans.X = q1x * q2w + q2x * q1w + cx;
            ans.Y = q1y * q2w + q2y * q1w + cy;
            ans.Z = q1z * q2w + q2z * q1w + cz;
            ans.W = q1w * q2w - dot;

            return ans;
        }

        public static Quat Multiply(Quat value1, float value2) {
            Quat ans;
            ans.X = value1.X * value2;
            ans.Y = value1.Y * value2;
            ans.Z = value1.Z * value2;
            ans.W = value1.W * value2;
            return ans;
        }

        public static Quat Divide(Quat value1, Quat value2) {
            Quat ans;

            var q1x = value1.X;
            var q1y = value1.Y;
            var q1z = value1.Z;
            var q1w = value1.W;

            //-------------------------------------
            // Inverse part.
            var ls = value2.X * value2.X + value2.Y * value2.Y +
                    value2.Z * value2.Z + value2.W * value2.W;
            var invNorm = 1f / ls;

            var q2x = -value2.X * invNorm;
            var q2y = -value2.Y * invNorm;
            var q2z = -value2.Z * invNorm;
            var q2w = value2.W * invNorm;

            //-------------------------------------
            // Multiply part.

            // cross(av, bv)
            var cx = q1y * q2z - q1z * q2y;
            var cy = q1z * q2x - q1x * q2z;
            var cz = q1x * q2y - q1y * q2x;

            var dot = q1x * q2x + q1y * q2y + q1z * q2z;

            ans.X = q1x * q2w + q2x * q1w + cx;
            ans.Y = q1y * q2w + q2y * q1w + cy;
            ans.Z = q1z * q2w + q2z * q1w + cz;
            ans.W = q1w * q2w - dot;

            return ans;
        }

        public static Quat operator -(Quat value) {
            Quat ans;
            ans.X = -value.X;
            ans.Y = -value.Y;
            ans.Z = -value.Z;
            ans.W = -value.W;
            return ans;
        }

        public static Quat operator +(Quat value1, Quat value2) {
            Quat ans;
            ans.X = value1.X + value2.X;
            ans.Y = value1.Y + value2.Y;
            ans.Z = value1.Z + value2.Z;
            ans.W = value1.W + value2.W;
            return ans;
        }

        public static Quat operator -(Quat value1, Quat value2) {
            Quat ans;
            ans.X = value1.X - value2.X;
            ans.Y = value1.Y - value2.Y;
            ans.Z = value1.Z - value2.Z;
            ans.W = value1.W - value2.W;
            return ans;
        }

        public static Quat operator *(Quat value1, Quat value2) {
            Quat ans;
            var q1x = value1.X;
            var q1y = value1.Y;
            var q1z = value1.Z;
            var q1w = value1.W;

            var q2x = value2.X;
            var q2y = value2.Y;
            var q2z = value2.Z;
            var q2w = value2.W;

            // cross(av, bv)
            var cx = q1y * q2z - q1z * q2y;
            var cy = q1z * q2x - q1x * q2z;
            var cz = q1x * q2y - q1y * q2x;

            var dot = q1x * q2x + q1y * q2y + q1z * q2z;
            ans.X = q1x * q2w + q2x * q1w + cx;
            ans.Y = q1y * q2w + q2y * q1w + cy;
            ans.Z = q1z * q2w + q2z * q1w + cz;
            ans.W = q1w * q2w - dot;
            return ans;
        }

        public static Quat operator *(Quat value1, float value2) {
            Quat ans;
            ans.X = value1.X * value2;
            ans.Y = value1.Y * value2;
            ans.Z = value1.Z * value2;
            ans.W = value1.W * value2;
            return ans;
        }

        public static Quat operator /(Quat value1, Quat value2) {
            Quat ans;

            var q1x = value1.X;
            var q1y = value1.Y;
            var q1z = value1.Z;
            var q1w = value1.W;

            //-------------------------------------
            // Inverse part.
            var ls = value2.X * value2.X + value2.Y * value2.Y +
                    value2.Z * value2.Z + value2.W * value2.W;
            var invNorm = 1f / ls;

            var q2x = -value2.X * invNorm;
            var q2y = -value2.Y * invNorm;
            var q2z = -value2.Z * invNorm;
            var q2w = value2.W * invNorm;

            //-------------------------------------
            // Multiply part.

            // cross(av, bv)
            var cx = q1y * q2z - q1z * q2y;
            var cy = q1z * q2x - q1x * q2z;
            var cz = q1x * q2y - q1y * q2x;

            var dot = q1x * q2x + q1y * q2y + q1z * q2z;

            ans.X = q1x * q2w + q2x * q1w + cx;
            ans.Y = q1y * q2w + q2y * q1w + cy;
            ans.Z = q1z * q2w + q2z * q1w + cz;
            ans.W = q1w * q2w - dot;

            return ans;
        }

        public static bool operator ==(Quat value1, Quat value2) {
            return value1.X == value2.X && value1.Y == value2.Y && value1.Z == value2.Z && value1.W == value2.W;
        }

        public static bool operator !=(Quat value1, Quat value2) {
            return value1.X != value2.X || value1.Y != value2.Y || value1.Z != value2.Z || value1.W != value2.W;
        }

        public bool Equals(Quat other) {
            return X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        }

        public override bool Equals(object obj) {
            return obj is Quat quaternion && Equals(quaternion);
        }

        public override string ToString() {
            var ci = CultureInfo.CurrentCulture;
            return string.Format(ci, "{{X:{0} Y:{1} Z:{2} W:{3}}}", X.ToString(ci), Y.ToString(ci), Z.ToString(ci), W.ToString(ci));
        }

        public override int GetHashCode() {
            return X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode() + W.GetHashCode();
        }
    }
}