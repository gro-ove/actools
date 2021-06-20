// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Security;

namespace AcTools.Numerics {
    public struct Mat4x4 : IEquatable<Mat4x4> {
        public float M11;
        public float M12;
        public float M13;
        public float M14;
        public float M21;
        public float M22;
        public float M23;
        public float M24;
        public float M31;
        public float M32;
        public float M33;
        public float M34;
        public float M41;
        public float M42;
        public float M43;
        public float M44;

        public static Mat4x4 Identity { get; } = new Mat4x4(
                1f, 0f, 0f, 0f,
                0f, 1f, 0f, 0f,
                0f, 0f, 1f, 0f,
                0f, 0f, 0f, 1f);

        public bool IsIdentity => M11 == 1f && M22 == 1f && M33 == 1f && M44 == 1f && // Check diagonal element first for early out.
                M12 == 0f && M13 == 0f && M14 == 0f &&
                M21 == 0f && M23 == 0f && M24 == 0f &&
                M31 == 0f && M32 == 0f && M34 == 0f &&
                M41 == 0f && M42 == 0f && M43 == 0f;

        public Vec3 Translation {
            get => new Vec3(M41, M42, M43);
            set {
                M41 = value.X;
                M42 = value.Y;
                M43 = value.Z;
            }
        }

        public Mat4x4(float m11, float m12, float m13, float m14,
                float m21, float m22, float m23, float m24,
                float m31, float m32, float m33, float m34,
                float m41, float m42, float m43, float m44) {
            M11 = m11;
            M12 = m12;
            M13 = m13;
            M14 = m14;
            M21 = m21;
            M22 = m22;
            M23 = m23;
            M24 = m24;
            M31 = m31;
            M32 = m32;
            M33 = m33;
            M34 = m34;
            M41 = m41;
            M42 = m42;
            M43 = m43;
            M44 = m44;
        }

        public static Mat4x4 CreateBillboard(Vec3 objectPosition, Vec3 cameraPosition, Vec3 cameraUpVector, Vec3 cameraForwardVector) {
            const float epsilon = 1e-4f;

            var zaxis = new Vec3(
                    objectPosition.X - cameraPosition.X,
                    objectPosition.Y - cameraPosition.Y,
                    objectPosition.Z - cameraPosition.Z);

            var norm = zaxis.LengthSquared();

            if (norm < epsilon) {
                zaxis = -cameraForwardVector;
            } else {
                zaxis = Vec3.Multiply(zaxis, 1f / (float)Math.Sqrt(norm));
            }

            var xaxis = Vec3.Normalize(Vec3.Cross(cameraUpVector, zaxis));

            var yaxis = Vec3.Cross(zaxis, xaxis);

            Mat4x4 result;

            result.M11 = xaxis.X;
            result.M12 = xaxis.Y;
            result.M13 = xaxis.Z;
            result.M14 = 0f;
            result.M21 = yaxis.X;
            result.M22 = yaxis.Y;
            result.M23 = yaxis.Z;
            result.M24 = 0f;
            result.M31 = zaxis.X;
            result.M32 = zaxis.Y;
            result.M33 = zaxis.Z;
            result.M34 = 0f;

            result.M41 = objectPosition.X;
            result.M42 = objectPosition.Y;
            result.M43 = objectPosition.Z;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateConstrainedBillboard(Vec3 objectPosition, Vec3 cameraPosition, Vec3 rotateAxis, Vec3 cameraForwardVector,
                Vec3 objectForwardVector) {
            const float epsilon = 1e-4f;
            const float minAngle = 1f - 0.1f * ((float)Math.PI / 180f); // 0.1 degrees

            // Treat the case when object and camera positions are too close.
            var faceDir = new Vec3(
                    objectPosition.X - cameraPosition.X,
                    objectPosition.Y - cameraPosition.Y,
                    objectPosition.Z - cameraPosition.Z);

            var norm = faceDir.LengthSquared();

            if (norm < epsilon) {
                faceDir = -cameraForwardVector;
            } else {
                faceDir = Vec3.Multiply(faceDir, 1f / (float)Math.Sqrt(norm));
            }

            var yaxis = rotateAxis;
            Vec3 xaxis;
            Vec3 zaxis;

            // Treat the case when angle between faceDir and rotateAxis is too close to 0.
            var dot = Vec3.Dot(rotateAxis, faceDir);

            if (Math.Abs(dot) > minAngle) {
                zaxis = objectForwardVector;

                // Make sure passed values are useful for compute.
                dot = Vec3.Dot(rotateAxis, zaxis);

                if (Math.Abs(dot) > minAngle) {
                    zaxis = Math.Abs(rotateAxis.Z) > minAngle ? new Vec3(1, 0, 0) : new Vec3(0, 0, -1);
                }

                xaxis = Vec3.Normalize(Vec3.Cross(rotateAxis, zaxis));
                zaxis = Vec3.Normalize(Vec3.Cross(xaxis, rotateAxis));
            } else {
                xaxis = Vec3.Normalize(Vec3.Cross(rotateAxis, faceDir));
                zaxis = Vec3.Normalize(Vec3.Cross(xaxis, yaxis));
            }

            Mat4x4 result;

            result.M11 = xaxis.X;
            result.M12 = xaxis.Y;
            result.M13 = xaxis.Z;
            result.M14 = 0f;
            result.M21 = yaxis.X;
            result.M22 = yaxis.Y;
            result.M23 = yaxis.Z;
            result.M24 = 0f;
            result.M31 = zaxis.X;
            result.M32 = zaxis.Y;
            result.M33 = zaxis.Z;
            result.M34 = 0f;

            result.M41 = objectPosition.X;
            result.M42 = objectPosition.Y;
            result.M43 = objectPosition.Z;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateTranslation(Vec3 position) {
            Mat4x4 result;

            result.M11 = 1f;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = 1f;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = 1f;
            result.M34 = 0f;

            result.M41 = position.X;
            result.M42 = position.Y;
            result.M43 = position.Z;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateTranslation(float xPosition, float yPosition, float zPosition) {
            Mat4x4 result;

            result.M11 = 1f;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = 1f;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = 1f;
            result.M34 = 0f;

            result.M41 = xPosition;
            result.M42 = yPosition;
            result.M43 = zPosition;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateScale(float xScale, float yScale, float zScale) {
            Mat4x4 result;

            result.M11 = xScale;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = yScale;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = zScale;
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateScale(float xScale, float yScale, float zScale, Vec3 centerPoint) {
            Mat4x4 result;

            var tx = centerPoint.X * (1 - xScale);
            var ty = centerPoint.Y * (1 - yScale);
            var tz = centerPoint.Z * (1 - zScale);

            result.M11 = xScale;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = yScale;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = zScale;
            result.M34 = 0f;
            result.M41 = tx;
            result.M42 = ty;
            result.M43 = tz;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateScale(Vec3 scales) {
            Mat4x4 result;

            result.M11 = scales.X;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = scales.Y;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = scales.Z;
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateScale(Vec3 scales, Vec3 centerPoint) {
            Mat4x4 result;

            var tx = centerPoint.X * (1 - scales.X);
            var ty = centerPoint.Y * (1 - scales.Y);
            var tz = centerPoint.Z * (1 - scales.Z);

            result.M11 = scales.X;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = scales.Y;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = scales.Z;
            result.M34 = 0f;
            result.M41 = tx;
            result.M42 = ty;
            result.M43 = tz;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateScale(float scale) {
            Mat4x4 result;

            result.M11 = scale;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = scale;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = scale;
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateScale(float scale, Vec3 centerPoint) {
            Mat4x4 result;

            var tx = centerPoint.X * (1 - scale);
            var ty = centerPoint.Y * (1 - scale);
            var tz = centerPoint.Z * (1 - scale);

            result.M11 = scale;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = scale;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = scale;
            result.M34 = 0f;
            result.M41 = tx;
            result.M42 = ty;
            result.M43 = tz;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateRotationX(float radians) {
            Mat4x4 result;

            var c = (float)Math.Cos(radians);
            var s = (float)Math.Sin(radians);

            // [  1  0  0  0 ]
            // [  0  c  s  0 ]
            // [  0 -s  c  0 ]
            // [  0  0  0  1 ]
            result.M11 = 1f;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = c;
            result.M23 = s;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = -s;
            result.M33 = c;
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateRotationX(float radians, Vec3 centerPoint) {
            Mat4x4 result;

            var c = (float)Math.Cos(radians);
            var s = (float)Math.Sin(radians);

            var y = centerPoint.Y * (1 - c) + centerPoint.Z * s;
            var z = centerPoint.Z * (1 - c) - centerPoint.Y * s;

            // [  1  0  0  0 ]
            // [  0  c  s  0 ]
            // [  0 -s  c  0 ]
            // [  0  y  z  1 ]
            result.M11 = 1f;
            result.M12 = 0f;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = c;
            result.M23 = s;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = -s;
            result.M33 = c;
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = y;
            result.M43 = z;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateRotationY(float radians) {
            Mat4x4 result;

            var c = (float)Math.Cos(radians);
            var s = (float)Math.Sin(radians);

            // [  c  0 -s  0 ]
            // [  0  1  0  0 ]
            // [  s  0  c  0 ]
            // [  0  0  0  1 ]
            result.M11 = c;
            result.M12 = 0f;
            result.M13 = -s;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = 1f;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = s;
            result.M32 = 0f;
            result.M33 = c;
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateRotationY(float radians, Vec3 centerPoint) {
            Mat4x4 result;

            var c = (float)Math.Cos(radians);
            var s = (float)Math.Sin(radians);

            var x = centerPoint.X * (1 - c) - centerPoint.Z * s;
            var z = centerPoint.Z * (1 - c) + centerPoint.X * s;

            // [  c  0 -s  0 ]
            // [  0  1  0  0 ]
            // [  s  0  c  0 ]
            // [  x  0  z  1 ]
            result.M11 = c;
            result.M12 = 0f;
            result.M13 = -s;
            result.M14 = 0f;
            result.M21 = 0f;
            result.M22 = 1f;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = s;
            result.M32 = 0f;
            result.M33 = c;
            result.M34 = 0f;
            result.M41 = x;
            result.M42 = 0f;
            result.M43 = z;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateRotationZ(float radians) {
            Mat4x4 result;

            var c = (float)Math.Cos(radians);
            var s = (float)Math.Sin(radians);

            // [  c  s  0  0 ]
            // [ -s  c  0  0 ]
            // [  0  0  1  0 ]
            // [  0  0  0  1 ]
            result.M11 = c;
            result.M12 = s;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = -s;
            result.M22 = c;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = 1f;
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateRotationZ(float radians, Vec3 centerPoint) {
            Mat4x4 result;

            var c = (float)Math.Cos(radians);
            var s = (float)Math.Sin(radians);

            var x = centerPoint.X * (1 - c) + centerPoint.Y * s;
            var y = centerPoint.Y * (1 - c) - centerPoint.X * s;

            // [  c  s  0  0 ]
            // [ -s  c  0  0 ]
            // [  0  0  1  0 ]
            // [  x  y  0  1 ]
            result.M11 = c;
            result.M12 = s;
            result.M13 = 0f;
            result.M14 = 0f;
            result.M21 = -s;
            result.M22 = c;
            result.M23 = 0f;
            result.M24 = 0f;
            result.M31 = 0f;
            result.M32 = 0f;
            result.M33 = 1f;
            result.M34 = 0f;
            result.M41 = x;
            result.M42 = y;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateFromAxisAngle(Vec3 axis, float angle) {
            // a: angle
            // x, y, z: unit vector for axis.
            //
            // Rotation matrix M can compute by using below equation.
            //
            //        T               T
            //  M = uu + (cos a)( I-uu ) + (sin a)S
            //
            // Where:
            //
            //  u = ( x, y, z )
            //
            //      [  0 -z  y ]
            //  S = [  z  0 -x ]
            //      [ -y  x  0 ]
            //
            //      [ 1 0 0 ]
            //  I = [ 0 1 0 ]
            //      [ 0 0 1 ]
            //
            //
            //     [  xx+cosa*(1-xx)   yx-cosa*yx-sina*z zx-cosa*xz+sina*y ]
            // M = [ xy-cosa*yx+sina*z    yy+cosa(1-yy)  yz-cosa*yz-sina*x ]
            //     [ zx-cosa*zx-sina*y zy-cosa*zy+sina*x   zz+cosa*(1-zz)  ]
            //
            float x = axis.X, y = axis.Y, z = axis.Z;
            float sa = (float)Math.Sin(angle), ca = (float)Math.Cos(angle);
            float xx = x * x, yy = y * y, zz = z * z;
            float xy = x * y, xz = x * z, yz = y * z;

            Mat4x4 result;

            result.M11 = xx + ca * (1f - xx);
            result.M12 = xy - ca * xy + sa * z;
            result.M13 = xz - ca * xz - sa * y;
            result.M14 = 0f;
            result.M21 = xy - ca * xy - sa * z;
            result.M22 = yy + ca * (1f - yy);
            result.M23 = yz - ca * yz + sa * x;
            result.M24 = 0f;
            result.M31 = xz - ca * xz + sa * y;
            result.M32 = yz - ca * yz - sa * x;
            result.M33 = zz + ca * (1f - zz);
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance) {
            if (fieldOfView <= 0f || fieldOfView >= Math.PI)
                throw new ArgumentOutOfRangeException(nameof(fieldOfView));

            if (nearPlaneDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(nearPlaneDistance));

            if (farPlaneDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(farPlaneDistance));

            if (nearPlaneDistance >= farPlaneDistance)
                throw new ArgumentOutOfRangeException(nameof(nearPlaneDistance));

            var yScale = 1f / (float)Math.Tan(fieldOfView * 0.5f);
            var xScale = yScale / aspectRatio;

            Mat4x4 result;

            result.M11 = xScale;
            result.M12 = result.M13 = result.M14 = 0f;

            result.M22 = yScale;
            result.M21 = result.M23 = result.M24 = 0f;

            result.M31 = result.M32 = 0f;
            result.M33 = farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
            result.M34 = -1f;

            result.M41 = result.M42 = result.M44 = 0f;
            result.M43 = nearPlaneDistance * farPlaneDistance / (nearPlaneDistance - farPlaneDistance);

            return result;
        }

        public static Mat4x4 CreatePerspective(float width, float height, float nearPlaneDistance, float farPlaneDistance) {
            if (nearPlaneDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(nearPlaneDistance));

            if (farPlaneDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(farPlaneDistance));

            if (nearPlaneDistance >= farPlaneDistance)
                throw new ArgumentOutOfRangeException(nameof(nearPlaneDistance));

            Mat4x4 result;

            result.M11 = 2f * nearPlaneDistance / width;
            result.M12 = result.M13 = result.M14 = 0f;

            result.M22 = 2f * nearPlaneDistance / height;
            result.M21 = result.M23 = result.M24 = 0f;

            result.M33 = farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
            result.M31 = result.M32 = 0f;
            result.M34 = -1f;

            result.M41 = result.M42 = result.M44 = 0f;
            result.M43 = nearPlaneDistance * farPlaneDistance / (nearPlaneDistance - farPlaneDistance);

            return result;
        }

        public static Mat4x4 CreatePerspectiveOffCenter(float left, float right, float bottom, float top, float nearPlaneDistance, float farPlaneDistance) {
            if (nearPlaneDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(nearPlaneDistance));

            if (farPlaneDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(farPlaneDistance));

            if (nearPlaneDistance >= farPlaneDistance)
                throw new ArgumentOutOfRangeException(nameof(nearPlaneDistance));

            Mat4x4 result;

            result.M11 = 2f * nearPlaneDistance / (right - left);
            result.M12 = result.M13 = result.M14 = 0f;

            result.M22 = 2f * nearPlaneDistance / (top - bottom);
            result.M21 = result.M23 = result.M24 = 0f;

            result.M31 = (left + right) / (right - left);
            result.M32 = (top + bottom) / (top - bottom);
            result.M33 = farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
            result.M34 = -1f;

            result.M43 = nearPlaneDistance * farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
            result.M41 = result.M42 = result.M44 = 0f;

            return result;
        }

        public static Mat4x4 CreateOrthographic(float width, float height, float zNearPlane, float zFarPlane) {
            Mat4x4 result;

            result.M11 = 2f / width;
            result.M12 = result.M13 = result.M14 = 0f;

            result.M22 = 2f / height;
            result.M21 = result.M23 = result.M24 = 0f;

            result.M33 = 1f / (zNearPlane - zFarPlane);
            result.M31 = result.M32 = result.M34 = 0f;

            result.M41 = result.M42 = 0f;
            result.M43 = zNearPlane / (zNearPlane - zFarPlane);
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNearPlane, float zFarPlane) {
            Mat4x4 result;

            result.M11 = 2f / (right - left);
            result.M12 = result.M13 = result.M14 = 0f;

            result.M22 = 2f / (top - bottom);
            result.M21 = result.M23 = result.M24 = 0f;

            result.M33 = 1f / (zNearPlane - zFarPlane);
            result.M31 = result.M32 = result.M34 = 0f;

            result.M41 = (left + right) / (left - right);
            result.M42 = (top + bottom) / (bottom - top);
            result.M43 = zNearPlane / (zNearPlane - zFarPlane);
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateLookAt(Vec3 cameraPosition, Vec3 cameraTarget, Vec3 cameraUpVector) {
            var zaxis = Vec3.Normalize(cameraPosition - cameraTarget);
            var xaxis = Vec3.Normalize(Vec3.Cross(cameraUpVector, zaxis));
            var yaxis = Vec3.Cross(zaxis, xaxis);

            Mat4x4 result;

            result.M11 = xaxis.X;
            result.M12 = yaxis.X;
            result.M13 = zaxis.X;
            result.M14 = 0f;
            result.M21 = xaxis.Y;
            result.M22 = yaxis.Y;
            result.M23 = zaxis.Y;
            result.M24 = 0f;
            result.M31 = xaxis.Z;
            result.M32 = yaxis.Z;
            result.M33 = zaxis.Z;
            result.M34 = 0f;
            result.M41 = -Vec3.Dot(xaxis, cameraPosition);
            result.M42 = -Vec3.Dot(yaxis, cameraPosition);
            result.M43 = -Vec3.Dot(zaxis, cameraPosition);
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateWorld(Vec3 position, Vec3 forward, Vec3 up) {
            var zaxis = Vec3.Normalize(-forward);
            var xaxis = Vec3.Normalize(Vec3.Cross(up, zaxis));
            var yaxis = Vec3.Cross(zaxis, xaxis);

            Mat4x4 result;

            result.M11 = xaxis.X;
            result.M12 = xaxis.Y;
            result.M13 = xaxis.Z;
            result.M14 = 0f;
            result.M21 = yaxis.X;
            result.M22 = yaxis.Y;
            result.M23 = yaxis.Z;
            result.M24 = 0f;
            result.M31 = zaxis.X;
            result.M32 = zaxis.Y;
            result.M33 = zaxis.Z;
            result.M34 = 0f;
            result.M41 = position.X;
            result.M42 = position.Y;
            result.M43 = position.Z;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateFromQuaternion(Quat quat) {
            Mat4x4 result;

            var xx = quat.X * quat.X;
            var yy = quat.Y * quat.Y;
            var zz = quat.Z * quat.Z;

            var xy = quat.X * quat.Y;
            var wz = quat.Z * quat.W;
            var xz = quat.Z * quat.X;
            var wy = quat.Y * quat.W;
            var yz = quat.Y * quat.Z;
            var wx = quat.X * quat.W;

            result.M11 = 1f - 2f * (yy + zz);
            result.M12 = 2f * (xy + wz);
            result.M13 = 2f * (xz - wy);
            result.M14 = 0f;
            result.M21 = 2f * (xy - wz);
            result.M22 = 1f - 2f * (zz + xx);
            result.M23 = 2f * (yz + wx);
            result.M24 = 0f;
            result.M31 = 2f * (xz + wy);
            result.M32 = 2f * (yz - wx);
            result.M33 = 1f - 2f * (yy + xx);
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;

            return result;
        }

        public static Mat4x4 CreateFromYawPitchRoll(float yaw, float pitch, float roll) {
            var q = Quat.CreateFromYawPitchRoll(yaw, pitch, roll);

            return CreateFromQuaternion(q);
        }

        public static Mat4x4 CreateShadow(Vec3 lightDirection, Plane plane) {
            var p = Plane.Normalize(plane);

            var dot = p.Normal.X * lightDirection.X + p.Normal.Y * lightDirection.Y + p.Normal.Z * lightDirection.Z;
            var a = -p.Normal.X;
            var b = -p.Normal.Y;
            var c = -p.Normal.Z;
            var d = -p.D;

            Mat4x4 result;

            result.M11 = a * lightDirection.X + dot;
            result.M21 = b * lightDirection.X;
            result.M31 = c * lightDirection.X;
            result.M41 = d * lightDirection.X;

            result.M12 = a * lightDirection.Y;
            result.M22 = b * lightDirection.Y + dot;
            result.M32 = c * lightDirection.Y;
            result.M42 = d * lightDirection.Y;

            result.M13 = a * lightDirection.Z;
            result.M23 = b * lightDirection.Z;
            result.M33 = c * lightDirection.Z + dot;
            result.M43 = d * lightDirection.Z;

            result.M14 = 0f;
            result.M24 = 0f;
            result.M34 = 0f;
            result.M44 = dot;

            return result;
        }

        public static Mat4x4 CreateReflection(Plane value) {
            value = Plane.Normalize(value);

            var a = value.Normal.X;
            var b = value.Normal.Y;
            var c = value.Normal.Z;

            var fa = -2f * a;
            var fb = -2f * b;
            var fc = -2f * c;

            Mat4x4 result;

            result.M11 = fa * a + 1f;
            result.M12 = fb * a;
            result.M13 = fc * a;
            result.M14 = 0f;

            result.M21 = fa * b;
            result.M22 = fb * b + 1f;
            result.M23 = fc * b;
            result.M24 = 0f;

            result.M31 = fa * c;
            result.M32 = fb * c;
            result.M33 = fc * c + 1f;
            result.M34 = 0f;

            result.M41 = fa * value.D;
            result.M42 = fb * value.D;
            result.M43 = fc * value.D;
            result.M44 = 1f;

            return result;
        }

        public float GetDeterminant() {
            // | a b c d |     | f g h |     | e g h |     | e f h |     | e f g |
            // | e f g h | = a | j k l | - b | i k l | + c | i j l | - d | i j k |
            // | i j k l |     | n o p |     | m o p |     | m n p |     | m n o |
            // | m n o p |
            //
            //   | f g h |
            // a | j k l | = a ( f ( kp - lo ) - g ( jp - ln ) + h ( jo - kn ) )
            //   | n o p |
            //
            //   | e g h |
            // b | i k l | = b ( e ( kp - lo ) - g ( ip - lm ) + h ( io - km ) )
            //   | m o p |
            //
            //   | e f h |
            // c | i j l | = c ( e ( jp - ln ) - f ( ip - lm ) + h ( in - jm ) )
            //   | m n p |
            //
            //   | e f g |
            // d | i j k | = d ( e ( jo - kn ) - f ( io - km ) + g ( in - jm ) )
            //   | m n o |
            //
            // Cost of operation
            // 17 adds and 28 muls.
            //
            // add: 6 + 8 + 3 = 17
            // mul: 12 + 16 = 28

            float a = M11, b = M12, c = M13, d = M14;
            float e = M21, f = M22, g = M23, h = M24;
            float i = M31, j = M32, k = M33, l = M34;
            float m = M41, n = M42, o = M43, p = M44;

            var kp_lo = k * p - l * o;
            var jp_ln = j * p - l * n;
            var jo_kn = j * o - k * n;
            var ip_lm = i * p - l * m;
            var io_km = i * o - k * m;
            var in_jm = i * n - j * m;

            return a * (f * kp_lo - g * jp_ln + h * jo_kn) -
                    b * (e * kp_lo - g * ip_lm + h * io_km) +
                    c * (e * jp_ln - f * ip_lm + h * in_jm) -
                    d * (e * jo_kn - f * io_km + g * in_jm);
        }

        public static bool Invert(Mat4x4 matrix, out Mat4x4 result) {
            //                                       -1
            // If you have matrix M, inverse Matrix M   can compute
            //
            //     -1       1
            //    M   = --------- A
            //            det(M)
            //
            // A is adjugate (adjoint) of M, where,
            //
            //      T
            // A = C
            //
            // C is Cofactor matrix of M, where,
            //           i + j
            // C   = (-1)      * det(M  )
            //  ij                    ij
            //
            //     [ a b c d ]
            // M = [ e f g h ]
            //     [ i j k l ]
            //     [ m n o p ]
            //
            // First Row
            //           2 | f g h |
            // C   = (-1)  | j k l | = + ( f ( kp - lo ) - g ( jp - ln ) + h ( jo - kn ) )
            //  11         | n o p |
            //
            //           3 | e g h |
            // C   = (-1)  | i k l | = - ( e ( kp - lo ) - g ( ip - lm ) + h ( io - km ) )
            //  12         | m o p |
            //
            //           4 | e f h |
            // C   = (-1)  | i j l | = + ( e ( jp - ln ) - f ( ip - lm ) + h ( in - jm ) )
            //  13         | m n p |
            //
            //           5 | e f g |
            // C   = (-1)  | i j k | = - ( e ( jo - kn ) - f ( io - km ) + g ( in - jm ) )
            //  14         | m n o |
            //
            // Second Row
            //           3 | b c d |
            // C   = (-1)  | j k l | = - ( b ( kp - lo ) - c ( jp - ln ) + d ( jo - kn ) )
            //  21         | n o p |
            //
            //           4 | a c d |
            // C   = (-1)  | i k l | = + ( a ( kp - lo ) - c ( ip - lm ) + d ( io - km ) )
            //  22         | m o p |
            //
            //           5 | a b d |
            // C   = (-1)  | i j l | = - ( a ( jp - ln ) - b ( ip - lm ) + d ( in - jm ) )
            //  23         | m n p |
            //
            //           6 | a b c |
            // C   = (-1)  | i j k | = + ( a ( jo - kn ) - b ( io - km ) + c ( in - jm ) )
            //  24         | m n o |
            //
            // Third Row
            //           4 | b c d |
            // C   = (-1)  | f g h | = + ( b ( gp - ho ) - c ( fp - hn ) + d ( fo - gn ) )
            //  31         | n o p |
            //
            //           5 | a c d |
            // C   = (-1)  | e g h | = - ( a ( gp - ho ) - c ( ep - hm ) + d ( eo - gm ) )
            //  32         | m o p |
            //
            //           6 | a b d |
            // C   = (-1)  | e f h | = + ( a ( fp - hn ) - b ( ep - hm ) + d ( en - fm ) )
            //  33         | m n p |
            //
            //           7 | a b c |
            // C   = (-1)  | e f g | = - ( a ( fo - gn ) - b ( eo - gm ) + c ( en - fm ) )
            //  34         | m n o |
            //
            // Fourth Row
            //           5 | b c d |
            // C   = (-1)  | f g h | = - ( b ( gl - hk ) - c ( fl - hj ) + d ( fk - gj ) )
            //  41         | j k l |
            //
            //           6 | a c d |
            // C   = (-1)  | e g h | = + ( a ( gl - hk ) - c ( el - hi ) + d ( ek - gi ) )
            //  42         | i k l |
            //
            //           7 | a b d |
            // C   = (-1)  | e f h | = - ( a ( fl - hj ) - b ( el - hi ) + d ( ej - fi ) )
            //  43         | i j l |
            //
            //           8 | a b c |
            // C   = (-1)  | e f g | = + ( a ( fk - gj ) - b ( ek - gi ) + c ( ej - fi ) )
            //  44         | i j k |
            //
            // Cost of operation
            // 53 adds, 104 muls, and 1 div.
            float a = matrix.M11, b = matrix.M12, c = matrix.M13, d = matrix.M14;
            float e = matrix.M21, f = matrix.M22, g = matrix.M23, h = matrix.M24;
            float i = matrix.M31, j = matrix.M32, k = matrix.M33, l = matrix.M34;
            float m = matrix.M41, n = matrix.M42, o = matrix.M43, p = matrix.M44;

            var kp_lo = k * p - l * o;
            var jp_ln = j * p - l * n;
            var jo_kn = j * o - k * n;
            var ip_lm = i * p - l * m;
            var io_km = i * o - k * m;
            var in_jm = i * n - j * m;

            var a11 = +(f * kp_lo - g * jp_ln + h * jo_kn);
            var a12 = -(e * kp_lo - g * ip_lm + h * io_km);
            var a13 = +(e * jp_ln - f * ip_lm + h * in_jm);
            var a14 = -(e * jo_kn - f * io_km + g * in_jm);

            var det = a * a11 + b * a12 + c * a13 + d * a14;

            if (Math.Abs(det) < float.Epsilon) {
                result = new Mat4x4(float.NaN, float.NaN, float.NaN, float.NaN,
                        float.NaN, float.NaN, float.NaN, float.NaN,
                        float.NaN, float.NaN, float.NaN, float.NaN,
                        float.NaN, float.NaN, float.NaN, float.NaN);
                return false;
            }

            var invDet = 1f / det;

            result.M11 = a11 * invDet;
            result.M21 = a12 * invDet;
            result.M31 = a13 * invDet;
            result.M41 = a14 * invDet;

            result.M12 = -(b * kp_lo - c * jp_ln + d * jo_kn) * invDet;
            result.M22 = +(a * kp_lo - c * ip_lm + d * io_km) * invDet;
            result.M32 = -(a * jp_ln - b * ip_lm + d * in_jm) * invDet;
            result.M42 = +(a * jo_kn - b * io_km + c * in_jm) * invDet;

            var gp_ho = g * p - h * o;
            var fp_hn = f * p - h * n;
            var fo_gn = f * o - g * n;
            var ep_hm = e * p - h * m;
            var eo_gm = e * o - g * m;
            var en_fm = e * n - f * m;

            result.M13 = +(b * gp_ho - c * fp_hn + d * fo_gn) * invDet;
            result.M23 = -(a * gp_ho - c * ep_hm + d * eo_gm) * invDet;
            result.M33 = +(a * fp_hn - b * ep_hm + d * en_fm) * invDet;
            result.M43 = -(a * fo_gn - b * eo_gm + c * en_fm) * invDet;

            var gl_hk = g * l - h * k;
            var fl_hj = f * l - h * j;
            var fk_gj = f * k - g * j;
            var el_hi = e * l - h * i;
            var ek_gi = e * k - g * i;
            var ej_fi = e * j - f * i;

            result.M14 = -(b * gl_hk - c * fl_hj + d * fk_gj) * invDet;
            result.M24 = +(a * gl_hk - c * el_hi + d * ek_gi) * invDet;
            result.M34 = -(a * fl_hj - b * el_hi + d * ej_fi) * invDet;
            result.M44 = +(a * fk_gj - b * ek_gi + c * ej_fi) * invDet;

            return true;
        }

        struct CanonicalBasis {
            public Vec3 Row0;
            public Vec3 Row1;
            public Vec3 Row2;
        }

        [SecuritySafeCritical]
        struct VectorBasis {
            public unsafe Vec3* Element0;
            public unsafe Vec3* Element1;
            public unsafe Vec3* Element2;
        }

        [SecuritySafeCritical]
        public static bool Decompose(Mat4x4 matrix, out Vec3 scale, out Quat rotation, out Vec3 translation) {
            var result = true;

            unsafe {
                fixed (Vec3* scaleBase = &scale) {
                    var pfScales = (float*)scaleBase;
                    const float EPSILON = 0.0001f;
                    float det;

                    VectorBasis vectorBasis;
                    var pVectorBasis = (Vec3**)&vectorBasis;

                    var matTemp = Identity;
                    var canonicalBasis = new CanonicalBasis();
                    var pCanonicalBasis = &canonicalBasis.Row0;

                    canonicalBasis.Row0 = new Vec3(1f, 0f, 0f);
                    canonicalBasis.Row1 = new Vec3(0f, 1f, 0f);
                    canonicalBasis.Row2 = new Vec3(0f, 0f, 1f);

                    translation = new Vec3(
                            matrix.M41,
                            matrix.M42,
                            matrix.M43);

                    pVectorBasis[0] = (Vec3*)&matTemp.M11;
                    pVectorBasis[1] = (Vec3*)&matTemp.M21;
                    pVectorBasis[2] = (Vec3*)&matTemp.M31;

                    *pVectorBasis[0] = new Vec3(matrix.M11, matrix.M12, matrix.M13);
                    *pVectorBasis[1] = new Vec3(matrix.M21, matrix.M22, matrix.M23);
                    *pVectorBasis[2] = new Vec3(matrix.M31, matrix.M32, matrix.M33);

                    scale.X = pVectorBasis[0] -> Length();
                    scale.Y = pVectorBasis[1] -> Length();
                    scale.Z = pVectorBasis[2] -> Length();

                    uint a, b, c;

                    #region Ranking
                    float x = pfScales[0], y = pfScales[1], z = pfScales[2];
                    if (x < y) {
                        if (y < z) {
                            a = 2;
                            b = 1;
                            c = 0;
                        } else {
                            a = 1;

                            if (x < z) {
                                b = 2;
                                c = 0;
                            } else {
                                b = 0;
                                c = 2;
                            }
                        }
                    } else {
                        if (x < z) {
                            a = 2;
                            b = 0;
                            c = 1;
                        } else {
                            a = 0;

                            if (y < z) {
                                b = 2;
                                c = 1;
                            } else {
                                b = 1;
                                c = 2;
                            }
                        }
                    }
                    #endregion

                    if (pfScales[a] < EPSILON) {
                        *pVectorBasis[a] = pCanonicalBasis[a];
                    }

                    *pVectorBasis[a] = Vec3.Normalize(*pVectorBasis[a]);

                    if (pfScales[b] < EPSILON) {
                        uint cc;
                        var fAbsX = Math.Abs(pVectorBasis[a] -> X);
                        var fAbsY = Math.Abs(pVectorBasis[a] -> Y);
                        var fAbsZ = Math.Abs(pVectorBasis[a] -> Z);

                        #region Ranking
                        if (fAbsX < fAbsY) {
                            if (fAbsY < fAbsZ) {
                                cc = 0;
                            } else {
                                if (fAbsX < fAbsZ) {
                                    cc = 0;
                                } else {
                                    cc = 2;
                                }
                            }
                        } else {
                            if (fAbsX < fAbsZ) {
                                cc = 1;
                            } else {
                                if (fAbsY < fAbsZ) {
                                    cc = 1;
                                } else {
                                    cc = 2;
                                }
                            }
                        }
                        #endregion

                        *pVectorBasis[b] = Vec3.Cross(*pVectorBasis[a], *(pCanonicalBasis + cc));
                    }

                    *pVectorBasis[b] = Vec3.Normalize(*pVectorBasis[b]);

                    if (pfScales[c] < EPSILON) {
                        *pVectorBasis[c] = Vec3.Cross(*pVectorBasis[a], *pVectorBasis[b]);
                    }

                    *pVectorBasis[c] = Vec3.Normalize(*pVectorBasis[c]);

                    det = matTemp.GetDeterminant();

                    // use Kramer's rule to check for handedness of coordinate system
                    if (det < 0f) {
                        // switch coordinate system by negating the scale and inverting the basis vector on the x-axis
                        pfScales[a] = -pfScales[a];
                        *pVectorBasis[a] = -(*pVectorBasis[a]);

                        det = -det;
                    }

                    det -= 1f;
                    det *= det;

                    if (EPSILON < det) {
                        // Non-SRT matrix encountered
                        rotation = Quat.Identity;
                        result = false;
                    } else {
                        // generate the quaternion from the matrix
                        rotation = Quat.CreateFromRotationMatrix(matTemp);
                    }
                }
            }

            return result;
        }

        public static Mat4x4 Transform(Mat4x4 value, Quat rotation) {
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

            var q11 = 1f - yy2 - zz2;
            var q21 = xy2 - wz2;
            var q31 = xz2 + wy2;

            var q12 = xy2 + wz2;
            var q22 = 1f - xx2 - zz2;
            var q32 = yz2 - wx2;

            var q13 = xz2 - wy2;
            var q23 = yz2 + wx2;
            var q33 = 1f - xx2 - yy2;

            Mat4x4 result;

            // First row
            result.M11 = value.M11 * q11 + value.M12 * q21 + value.M13 * q31;
            result.M12 = value.M11 * q12 + value.M12 * q22 + value.M13 * q32;
            result.M13 = value.M11 * q13 + value.M12 * q23 + value.M13 * q33;
            result.M14 = value.M14;

            // Second row
            result.M21 = value.M21 * q11 + value.M22 * q21 + value.M23 * q31;
            result.M22 = value.M21 * q12 + value.M22 * q22 + value.M23 * q32;
            result.M23 = value.M21 * q13 + value.M22 * q23 + value.M23 * q33;
            result.M24 = value.M24;

            // Third row
            result.M31 = value.M31 * q11 + value.M32 * q21 + value.M33 * q31;
            result.M32 = value.M31 * q12 + value.M32 * q22 + value.M33 * q32;
            result.M33 = value.M31 * q13 + value.M32 * q23 + value.M33 * q33;
            result.M34 = value.M34;

            // Fourth row
            result.M41 = value.M41 * q11 + value.M42 * q21 + value.M43 * q31;
            result.M42 = value.M41 * q12 + value.M42 * q22 + value.M43 * q32;
            result.M43 = value.M41 * q13 + value.M42 * q23 + value.M43 * q33;
            result.M44 = value.M44;

            return result;
        }

        public static Mat4x4 Transpose(Mat4x4 matrix) {
            Mat4x4 result;

            result.M11 = matrix.M11;
            result.M12 = matrix.M21;
            result.M13 = matrix.M31;
            result.M14 = matrix.M41;
            result.M21 = matrix.M12;
            result.M22 = matrix.M22;
            result.M23 = matrix.M32;
            result.M24 = matrix.M42;
            result.M31 = matrix.M13;
            result.M32 = matrix.M23;
            result.M33 = matrix.M33;
            result.M34 = matrix.M43;
            result.M41 = matrix.M14;
            result.M42 = matrix.M24;
            result.M43 = matrix.M34;
            result.M44 = matrix.M44;

            return result;
        }

        public static Mat4x4 Lerp(Mat4x4 matrix1, Mat4x4 matrix2, float amount) {
            Mat4x4 result;

            // First row
            result.M11 = matrix1.M11 + (matrix2.M11 - matrix1.M11) * amount;
            result.M12 = matrix1.M12 + (matrix2.M12 - matrix1.M12) * amount;
            result.M13 = matrix1.M13 + (matrix2.M13 - matrix1.M13) * amount;
            result.M14 = matrix1.M14 + (matrix2.M14 - matrix1.M14) * amount;

            // Second row
            result.M21 = matrix1.M21 + (matrix2.M21 - matrix1.M21) * amount;
            result.M22 = matrix1.M22 + (matrix2.M22 - matrix1.M22) * amount;
            result.M23 = matrix1.M23 + (matrix2.M23 - matrix1.M23) * amount;
            result.M24 = matrix1.M24 + (matrix2.M24 - matrix1.M24) * amount;

            // Third row
            result.M31 = matrix1.M31 + (matrix2.M31 - matrix1.M31) * amount;
            result.M32 = matrix1.M32 + (matrix2.M32 - matrix1.M32) * amount;
            result.M33 = matrix1.M33 + (matrix2.M33 - matrix1.M33) * amount;
            result.M34 = matrix1.M34 + (matrix2.M34 - matrix1.M34) * amount;

            // Fourth row
            result.M41 = matrix1.M41 + (matrix2.M41 - matrix1.M41) * amount;
            result.M42 = matrix1.M42 + (matrix2.M42 - matrix1.M42) * amount;
            result.M43 = matrix1.M43 + (matrix2.M43 - matrix1.M43) * amount;
            result.M44 = matrix1.M44 + (matrix2.M44 - matrix1.M44) * amount;

            return result;
        }

        public static Mat4x4 Negate(Mat4x4 value) {
            Mat4x4 result;

            result.M11 = -value.M11;
            result.M12 = -value.M12;
            result.M13 = -value.M13;
            result.M14 = -value.M14;
            result.M21 = -value.M21;
            result.M22 = -value.M22;
            result.M23 = -value.M23;
            result.M24 = -value.M24;
            result.M31 = -value.M31;
            result.M32 = -value.M32;
            result.M33 = -value.M33;
            result.M34 = -value.M34;
            result.M41 = -value.M41;
            result.M42 = -value.M42;
            result.M43 = -value.M43;
            result.M44 = -value.M44;

            return result;
        }

        public static Mat4x4 Add(Mat4x4 value1, Mat4x4 value2) {
            Mat4x4 result;

            result.M11 = value1.M11 + value2.M11;
            result.M12 = value1.M12 + value2.M12;
            result.M13 = value1.M13 + value2.M13;
            result.M14 = value1.M14 + value2.M14;
            result.M21 = value1.M21 + value2.M21;
            result.M22 = value1.M22 + value2.M22;
            result.M23 = value1.M23 + value2.M23;
            result.M24 = value1.M24 + value2.M24;
            result.M31 = value1.M31 + value2.M31;
            result.M32 = value1.M32 + value2.M32;
            result.M33 = value1.M33 + value2.M33;
            result.M34 = value1.M34 + value2.M34;
            result.M41 = value1.M41 + value2.M41;
            result.M42 = value1.M42 + value2.M42;
            result.M43 = value1.M43 + value2.M43;
            result.M44 = value1.M44 + value2.M44;

            return result;
        }

        public static Mat4x4 Subtract(Mat4x4 value1, Mat4x4 value2) {
            Mat4x4 result;

            result.M11 = value1.M11 - value2.M11;
            result.M12 = value1.M12 - value2.M12;
            result.M13 = value1.M13 - value2.M13;
            result.M14 = value1.M14 - value2.M14;
            result.M21 = value1.M21 - value2.M21;
            result.M22 = value1.M22 - value2.M22;
            result.M23 = value1.M23 - value2.M23;
            result.M24 = value1.M24 - value2.M24;
            result.M31 = value1.M31 - value2.M31;
            result.M32 = value1.M32 - value2.M32;
            result.M33 = value1.M33 - value2.M33;
            result.M34 = value1.M34 - value2.M34;
            result.M41 = value1.M41 - value2.M41;
            result.M42 = value1.M42 - value2.M42;
            result.M43 = value1.M43 - value2.M43;
            result.M44 = value1.M44 - value2.M44;

            return result;
        }

        public static Mat4x4 Multiply(Mat4x4 value1, Mat4x4 value2) {
            Mat4x4 result;

            // First row
            result.M11 = value1.M11 * value2.M11 + value1.M12 * value2.M21 + value1.M13 * value2.M31 + value1.M14 * value2.M41;
            result.M12 = value1.M11 * value2.M12 + value1.M12 * value2.M22 + value1.M13 * value2.M32 + value1.M14 * value2.M42;
            result.M13 = value1.M11 * value2.M13 + value1.M12 * value2.M23 + value1.M13 * value2.M33 + value1.M14 * value2.M43;
            result.M14 = value1.M11 * value2.M14 + value1.M12 * value2.M24 + value1.M13 * value2.M34 + value1.M14 * value2.M44;

            // Second row
            result.M21 = value1.M21 * value2.M11 + value1.M22 * value2.M21 + value1.M23 * value2.M31 + value1.M24 * value2.M41;
            result.M22 = value1.M21 * value2.M12 + value1.M22 * value2.M22 + value1.M23 * value2.M32 + value1.M24 * value2.M42;
            result.M23 = value1.M21 * value2.M13 + value1.M22 * value2.M23 + value1.M23 * value2.M33 + value1.M24 * value2.M43;
            result.M24 = value1.M21 * value2.M14 + value1.M22 * value2.M24 + value1.M23 * value2.M34 + value1.M24 * value2.M44;

            // Third row
            result.M31 = value1.M31 * value2.M11 + value1.M32 * value2.M21 + value1.M33 * value2.M31 + value1.M34 * value2.M41;
            result.M32 = value1.M31 * value2.M12 + value1.M32 * value2.M22 + value1.M33 * value2.M32 + value1.M34 * value2.M42;
            result.M33 = value1.M31 * value2.M13 + value1.M32 * value2.M23 + value1.M33 * value2.M33 + value1.M34 * value2.M43;
            result.M34 = value1.M31 * value2.M14 + value1.M32 * value2.M24 + value1.M33 * value2.M34 + value1.M34 * value2.M44;

            // Fourth row
            result.M41 = value1.M41 * value2.M11 + value1.M42 * value2.M21 + value1.M43 * value2.M31 + value1.M44 * value2.M41;
            result.M42 = value1.M41 * value2.M12 + value1.M42 * value2.M22 + value1.M43 * value2.M32 + value1.M44 * value2.M42;
            result.M43 = value1.M41 * value2.M13 + value1.M42 * value2.M23 + value1.M43 * value2.M33 + value1.M44 * value2.M43;
            result.M44 = value1.M41 * value2.M14 + value1.M42 * value2.M24 + value1.M43 * value2.M34 + value1.M44 * value2.M44;

            return result;
        }

        public static Mat4x4 Multiply(Mat4x4 value1, float value2) {
            Mat4x4 result;

            result.M11 = value1.M11 * value2;
            result.M12 = value1.M12 * value2;
            result.M13 = value1.M13 * value2;
            result.M14 = value1.M14 * value2;
            result.M21 = value1.M21 * value2;
            result.M22 = value1.M22 * value2;
            result.M23 = value1.M23 * value2;
            result.M24 = value1.M24 * value2;
            result.M31 = value1.M31 * value2;
            result.M32 = value1.M32 * value2;
            result.M33 = value1.M33 * value2;
            result.M34 = value1.M34 * value2;
            result.M41 = value1.M41 * value2;
            result.M42 = value1.M42 * value2;
            result.M43 = value1.M43 * value2;
            result.M44 = value1.M44 * value2;

            return result;
        }

        public static Mat4x4 operator -(Mat4x4 value) {
            Mat4x4 m;

            m.M11 = -value.M11;
            m.M12 = -value.M12;
            m.M13 = -value.M13;
            m.M14 = -value.M14;
            m.M21 = -value.M21;
            m.M22 = -value.M22;
            m.M23 = -value.M23;
            m.M24 = -value.M24;
            m.M31 = -value.M31;
            m.M32 = -value.M32;
            m.M33 = -value.M33;
            m.M34 = -value.M34;
            m.M41 = -value.M41;
            m.M42 = -value.M42;
            m.M43 = -value.M43;
            m.M44 = -value.M44;

            return m;
        }

        public static Mat4x4 operator +(Mat4x4 value1, Mat4x4 value2) {
            Mat4x4 m;

            m.M11 = value1.M11 + value2.M11;
            m.M12 = value1.M12 + value2.M12;
            m.M13 = value1.M13 + value2.M13;
            m.M14 = value1.M14 + value2.M14;
            m.M21 = value1.M21 + value2.M21;
            m.M22 = value1.M22 + value2.M22;
            m.M23 = value1.M23 + value2.M23;
            m.M24 = value1.M24 + value2.M24;
            m.M31 = value1.M31 + value2.M31;
            m.M32 = value1.M32 + value2.M32;
            m.M33 = value1.M33 + value2.M33;
            m.M34 = value1.M34 + value2.M34;
            m.M41 = value1.M41 + value2.M41;
            m.M42 = value1.M42 + value2.M42;
            m.M43 = value1.M43 + value2.M43;
            m.M44 = value1.M44 + value2.M44;

            return m;
        }

        public static Mat4x4 operator -(Mat4x4 value1, Mat4x4 value2) {
            Mat4x4 m;

            m.M11 = value1.M11 - value2.M11;
            m.M12 = value1.M12 - value2.M12;
            m.M13 = value1.M13 - value2.M13;
            m.M14 = value1.M14 - value2.M14;
            m.M21 = value1.M21 - value2.M21;
            m.M22 = value1.M22 - value2.M22;
            m.M23 = value1.M23 - value2.M23;
            m.M24 = value1.M24 - value2.M24;
            m.M31 = value1.M31 - value2.M31;
            m.M32 = value1.M32 - value2.M32;
            m.M33 = value1.M33 - value2.M33;
            m.M34 = value1.M34 - value2.M34;
            m.M41 = value1.M41 - value2.M41;
            m.M42 = value1.M42 - value2.M42;
            m.M43 = value1.M43 - value2.M43;
            m.M44 = value1.M44 - value2.M44;

            return m;
        }

        public static Mat4x4 operator *(Mat4x4 value1, Mat4x4 value2) {
            Mat4x4 m;

            // First row
            m.M11 = value1.M11 * value2.M11 + value1.M12 * value2.M21 + value1.M13 * value2.M31 + value1.M14 * value2.M41;
            m.M12 = value1.M11 * value2.M12 + value1.M12 * value2.M22 + value1.M13 * value2.M32 + value1.M14 * value2.M42;
            m.M13 = value1.M11 * value2.M13 + value1.M12 * value2.M23 + value1.M13 * value2.M33 + value1.M14 * value2.M43;
            m.M14 = value1.M11 * value2.M14 + value1.M12 * value2.M24 + value1.M13 * value2.M34 + value1.M14 * value2.M44;

            // Second row
            m.M21 = value1.M21 * value2.M11 + value1.M22 * value2.M21 + value1.M23 * value2.M31 + value1.M24 * value2.M41;
            m.M22 = value1.M21 * value2.M12 + value1.M22 * value2.M22 + value1.M23 * value2.M32 + value1.M24 * value2.M42;
            m.M23 = value1.M21 * value2.M13 + value1.M22 * value2.M23 + value1.M23 * value2.M33 + value1.M24 * value2.M43;
            m.M24 = value1.M21 * value2.M14 + value1.M22 * value2.M24 + value1.M23 * value2.M34 + value1.M24 * value2.M44;

            // Third row
            m.M31 = value1.M31 * value2.M11 + value1.M32 * value2.M21 + value1.M33 * value2.M31 + value1.M34 * value2.M41;
            m.M32 = value1.M31 * value2.M12 + value1.M32 * value2.M22 + value1.M33 * value2.M32 + value1.M34 * value2.M42;
            m.M33 = value1.M31 * value2.M13 + value1.M32 * value2.M23 + value1.M33 * value2.M33 + value1.M34 * value2.M43;
            m.M34 = value1.M31 * value2.M14 + value1.M32 * value2.M24 + value1.M33 * value2.M34 + value1.M34 * value2.M44;

            // Fourth row
            m.M41 = value1.M41 * value2.M11 + value1.M42 * value2.M21 + value1.M43 * value2.M31 + value1.M44 * value2.M41;
            m.M42 = value1.M41 * value2.M12 + value1.M42 * value2.M22 + value1.M43 * value2.M32 + value1.M44 * value2.M42;
            m.M43 = value1.M41 * value2.M13 + value1.M42 * value2.M23 + value1.M43 * value2.M33 + value1.M44 * value2.M43;
            m.M44 = value1.M41 * value2.M14 + value1.M42 * value2.M24 + value1.M43 * value2.M34 + value1.M44 * value2.M44;

            return m;
        }

        public static Mat4x4 operator *(Mat4x4 value1, float value2) {
            Mat4x4 m;

            m.M11 = value1.M11 * value2;
            m.M12 = value1.M12 * value2;
            m.M13 = value1.M13 * value2;
            m.M14 = value1.M14 * value2;
            m.M21 = value1.M21 * value2;
            m.M22 = value1.M22 * value2;
            m.M23 = value1.M23 * value2;
            m.M24 = value1.M24 * value2;
            m.M31 = value1.M31 * value2;
            m.M32 = value1.M32 * value2;
            m.M33 = value1.M33 * value2;
            m.M34 = value1.M34 * value2;
            m.M41 = value1.M41 * value2;
            m.M42 = value1.M42 * value2;
            m.M43 = value1.M43 * value2;
            m.M44 = value1.M44 * value2;
            return m;
        }

        public static bool operator ==(Mat4x4 value1, Mat4x4 value2) {
            return value1.M11 == value2.M11 && value1.M22 == value2.M22 && value1.M33 == value2.M33 && value1.M44 == value2.M44
                    && // Check diagonal element first for early out.
                    value1.M12 == value2.M12 && value1.M13 == value2.M13 && value1.M14 == value2.M14 &&
                    value1.M21 == value2.M21 && value1.M23 == value2.M23 && value1.M24 == value2.M24 &&
                    value1.M31 == value2.M31 && value1.M32 == value2.M32 && value1.M34 == value2.M34 &&
                    value1.M41 == value2.M41 && value1.M42 == value2.M42 && value1.M43 == value2.M43;
        }

        public static bool operator !=(Mat4x4 value1, Mat4x4 value2) {
            return value1.M11 != value2.M11 || value1.M12 != value2.M12 || value1.M13 != value2.M13 || value1.M14 != value2.M14 ||
                    value1.M21 != value2.M21 || value1.M22 != value2.M22 || value1.M23 != value2.M23 || value1.M24 != value2.M24 ||
                    value1.M31 != value2.M31 || value1.M32 != value2.M32 || value1.M33 != value2.M33 || value1.M34 != value2.M34 ||
                    value1.M41 != value2.M41 || value1.M42 != value2.M42 || value1.M43 != value2.M43 || value1.M44 != value2.M44;
        }

        public bool Equals(Mat4x4 other) {
            return M11 == other.M11 && M22 == other.M22 && M33 == other.M33 && M44 == other.M44 && // Check diagonal element first for early out.
                    M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
                    M21 == other.M21 && M23 == other.M23 && M24 == other.M24 &&
                    M31 == other.M31 && M32 == other.M32 && M34 == other.M34 &&
                    M41 == other.M41 && M42 == other.M42 && M43 == other.M43;
        }

        public override bool Equals(object obj) {
            if (obj is Mat4x4) {
                return Equals((Mat4x4)obj);
            }

            return false;
        }

        public override string ToString() {
            var ci = CultureInfo.CurrentCulture;

            return string.Format(ci,
                    "{{ {{M11:{0} M12:{1} M13:{2} M14:{3}}} {{M21:{4} M22:{5} M23:{6} M24:{7}}} {{M31:{8} M32:{9} M33:{10} M34:{11}}} {{M41:{12} M42:{13} M43:{14} M44:{15}}} }}",
                    M11.ToString(ci), M12.ToString(ci), M13.ToString(ci), M14.ToString(ci),
                    M21.ToString(ci), M22.ToString(ci), M23.ToString(ci), M24.ToString(ci),
                    M31.ToString(ci), M32.ToString(ci), M33.ToString(ci), M34.ToString(ci),
                    M41.ToString(ci), M42.ToString(ci), M43.ToString(ci), M44.ToString(ci));
        }

        public override int GetHashCode() {
            return M11.GetHashCode() + M12.GetHashCode() + M13.GetHashCode() + M14.GetHashCode() +
                    M21.GetHashCode() + M22.GetHashCode() + M23.GetHashCode() + M24.GetHashCode() +
                    M31.GetHashCode() + M32.GetHashCode() + M33.GetHashCode() + M34.GetHashCode() +
                    M41.GetHashCode() + M42.GetHashCode() + M43.GetHashCode() + M44.GetHashCode();
        }
    }
}