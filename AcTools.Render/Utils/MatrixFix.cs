using System;
using System.Runtime.InteropServices;
using SlimDX;

namespace AcTools.Render.Utils {
    /** I still remember the times when Windows didn’t suck. */
    public static class MatrixFix {
        public static Matrix Invert_v2(this Matrix input) {
            var determinant = input.Determinant();
            if (Math.Abs(determinant) < float.Epsilon) {
                return Matrix.Identity;
            }
            var invDet = 1f / determinant;
            return new Matrix {
                M11 = invDet * (input.M22 * input.M33 * input.M44 + input.M23 * input.M34 * input.M42 + input.M24 * input.M32 * input.M43
                        - input.M24 * input.M33 * input.M42 - input.M23 * input.M32 * input.M44 - input.M22 * input.M34 * input.M43),
                M12 = invDet * (input.M12 * input.M34 * input.M43 + input.M13 * input.M32 * input.M44 + input.M14 * input.M33 * input.M42
                        - input.M14 * input.M32 * input.M43 - input.M13 * input.M34 * input.M42 - input.M12 * input.M33 * input.M44),
                M13 = invDet * (input.M12 * input.M23 * input.M44 + input.M13 * input.M24 * input.M42 + input.M14 * input.M22 * input.M43
                        - input.M14 * input.M23 * input.M42 - input.M13 * input.M22 * input.M44 - input.M12 * input.M24 * input.M43),
                M14 = invDet * (input.M12 * input.M24 * input.M33 + input.M13 * input.M22 * input.M34 + input.M14 * input.M23 * input.M32
                        - input.M14 * input.M22 * input.M33 - input.M13 * input.M24 * input.M32 - input.M12 * input.M23 * input.M34),
                M21 = invDet * (input.M21 * input.M34 * input.M43 + input.M23 * input.M31 * input.M44 + input.M24 * input.M33 * input.M41
                        - input.M24 * input.M31 * input.M43 - input.M23 * input.M34 * input.M41 - input.M21 * input.M33 * input.M44),
                M22 = invDet * (input.M11 * input.M33 * input.M44 + input.M13 * input.M34 * input.M41 + input.M14 * input.M31 * input.M43
                        - input.M14 * input.M33 * input.M41 - input.M13 * input.M31 * input.M44 - input.M11 * input.M34 * input.M43),
                M23 = invDet * (input.M11 * input.M24 * input.M43 + input.M13 * input.M21 * input.M44 + input.M14 * input.M23 * input.M41
                        - input.M14 * input.M21 * input.M43 - input.M13 * input.M24 * input.M41 - input.M11 * input.M23 * input.M44),
                M24 = invDet * (input.M11 * input.M23 * input.M34 + input.M13 * input.M24 * input.M31 + input.M14 * input.M21 * input.M33
                        - input.M14 * input.M23 * input.M31 - input.M13 * input.M21 * input.M34 - input.M11 * input.M24 * input.M33),
                M31 = invDet * (input.M21 * input.M32 * input.M44 + input.M22 * input.M34 * input.M41 + input.M24 * input.M31 * input.M42
                        - input.M24 * input.M32 * input.M41 - input.M22 * input.M31 * input.M44 - input.M21 * input.M34 * input.M42),
                M32 = invDet * (input.M11 * input.M34 * input.M42 + input.M12 * input.M31 * input.M44 + input.M14 * input.M32 * input.M41
                        - input.M14 * input.M31 * input.M42 - input.M12 * input.M34 * input.M41 - input.M11 * input.M32 * input.M44),
                M33 = invDet * (input.M11 * input.M22 * input.M44 + input.M12 * input.M24 * input.M41 + input.M14 * input.M21 * input.M42
                        - input.M14 * input.M22 * input.M41 - input.M12 * input.M21 * input.M44 - input.M11 * input.M24 * input.M42),
                M34 = invDet * (input.M11 * input.M24 * input.M32 + input.M12 * input.M21 * input.M34 + input.M14 * input.M22 * input.M31
                        - input.M14 * input.M21 * input.M32 - input.M12 * input.M24 * input.M31 - input.M11 * input.M22 * input.M34),
                M41 = invDet * (input.M21 * input.M33 * input.M42 + input.M22 * input.M31 * input.M43 + input.M23 * input.M32 * input.M41
                        - input.M23 * input.M31 * input.M42 - input.M22 * input.M33 * input.M41 - input.M21 * input.M32 * input.M43),
                M42 = invDet * (input.M11 * input.M32 * input.M43 + input.M12 * input.M33 * input.M41 + input.M13 * input.M31 * input.M42
                        - input.M13 * input.M32 * input.M41 - input.M12 * input.M31 * input.M43 - input.M11 * input.M33 * input.M42),
                M43 = invDet * (input.M11 * input.M23 * input.M42 + input.M12 * input.M21 * input.M43 + input.M13 * input.M22 * input.M41
                        - input.M13 * input.M21 * input.M42 - input.M12 * input.M23 * input.M41 - input.M11 * input.M22 * input.M43),
                M44 = invDet * (input.M11 * input.M22 * input.M33 + input.M12 * input.M23 * input.M31 + input.M13 * input.M21 * input.M32
                        - input.M13 * input.M22 * input.M31 - input.M12 * input.M21 * input.M33 - input.M11 * input.M23 * input.M32),
            };
        }

        [return: MarshalAs(UnmanagedType.U1)]
        public static bool Decompose_v2(this Matrix matrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation) {
            translation = new Vector3(matrix.M41, matrix.M42, matrix.M43);
            scale = new Vector3(
                    new Vector3(matrix.M11, matrix.M12, matrix.M13).Length(),
                    new Vector3(matrix.M21, matrix.M22, matrix.M23).Length(),
                    new Vector3(matrix.M31, matrix.M32, matrix.M33).Length());
            if (Math.Abs(scale.X) < float.Epsilon || Math.Abs(scale.Y) < float.Epsilon || Math.Abs(scale.Z) < float.Epsilon) {
                rotation = Quaternion.Identity;
                return false;
            }

            var m1 = new Vector3(matrix.M11, matrix.M12, matrix.M13) / scale.X;
            var m2 = new Vector3(matrix.M21, matrix.M22, matrix.M23) / scale.Y;
            var m3 = new Vector3(matrix.M31, matrix.M32, matrix.M33) / scale.Z;
            var rotationMatrix = new Matrix {
                M11 = m1.X,
                M12 = m1.Y,
                M13 = m1.Z,
                M14 = 0,
                M21 = m2.X,
                M22 = m2.Y,
                M23 = m2.Z,
                M24 = 0,
                M31 = m3.X,
                M32 = m3.Y,
                M33 = m3.Z,
                M34 = 0,
                M41 = 0,
                M42 = 0,
                M43 = 0,
                M44 = 1
            };
            Quaternion.RotationMatrix(ref rotationMatrix, out rotation);
            return true;
        }

        public static Matrix LookAtLH(Vector3 eye, Vector3 target, Vector3 up) {
            var zAxis = Vector3.Normalize(target - eye);
            var xAxis = Vector3.Normalize(Vector3.Cross(up, zAxis));
            var yAxis = Vector3.Cross(zAxis, xAxis);
            return new Matrix {
                M11 = xAxis.X,
                M12 = yAxis.X,
                M13 = zAxis.X,
                M14 = 0.0f,
                M21 = xAxis.Y,
                M22 = yAxis.Y,
                M23 = zAxis.Y,
                M24 = 0.0f,
                M31 = xAxis.Z,
                M32 = yAxis.Z,
                M33 = zAxis.Z,
                M34 = 0.0f,
                M41 = -Vector3.Dot(xAxis, eye),
                M42 = -Vector3.Dot(yAxis, eye),
                M43 = -Vector3.Dot(zAxis, eye),
                M44 = 1.0f
            };
        }

        public static Matrix LookAtRH(Vector3 eye, Vector3 target, Vector3 up) {
            var zAxis = Vector3.Normalize(eye - target);
            var xAxis = Vector3.Normalize(Vector3.Cross(up, zAxis));
            var yAxis = Vector3.Cross(zAxis, xAxis);
            return new Matrix {
                M11 = xAxis.X,
                M12 = yAxis.X,
                M13 = zAxis.X,
                M14 = 0.0f,
                M21 = xAxis.Y,
                M22 = yAxis.Y,
                M23 = zAxis.Y,
                M24 = 0.0f,
                M31 = xAxis.Z,
                M32 = yAxis.Z,
                M33 = zAxis.Z,
                M34 = 0.0f,
                M41 = -Vector3.Dot(xAxis, eye),
                M42 = -Vector3.Dot(yAxis, eye),
                M43 = -Vector3.Dot(zAxis, eye),
                M44 = 1.0f
            };
        }

        public static Matrix OrthoRH(float width, float height, float znear, float zfar) {
            var range = 1.0f / (znear - zfar);
            return new Matrix {
                M11 = 2.0f / width,
                M12 = 0.0f,
                M13 = 0.0f,
                M14 = 0.0f,
                M21 = 0.0f,
                M22 = 2.0f / height,
                M23 = 0.0f,
                M24 = 0.0f,
                M31 = 0.0f,
                M32 = 0.0f,
                M33 = range,
                M34 = 0.0f,
                M41 = 0.0f,
                M42 = 0.0f,
                M43 = znear * range,
                M44 = 1.0f
            };
        }

        public static Matrix AffineTransformation2D(float scaling, Vector2 rotationCenter, float rotation, Vector2 translation) {
            var scaleMatrix = Matrix.Scaling(scaling, scaling, 1.0f);
            var rotationMatrix = Matrix.RotationZ(rotation);
            var translationMatrix = Matrix.Translation(translation.X, translation.Y, 0.0f);
            var centerTranslation = Matrix.Translation(rotationCenter.X, rotationCenter.Y, 0.0f);
            var centerInverse = Matrix.Translation(-rotationCenter.X, -rotationCenter.Y, 0.0f);
            return centerInverse * scaleMatrix * rotationMatrix * centerTranslation * translationMatrix;
        }

        public static Matrix Transformation2D(Vector2 scalingCenter, float scalingRotation, Vector2 scaling, Vector2 rotationCenter, 
                float rotation, Vector2 translation) {
            var scaleMatrix = Matrix.Scaling(scaling.X, scaling.Y, 1.0f);

            if (Math.Abs(scalingRotation) > float.Epsilon) {
                var scalingRotationMatrix = Matrix.RotationZ(scalingRotation);
                var toCenter = Matrix.Translation(scalingCenter.X, scalingCenter.Y, 0.0f);
                var fromCenter = Matrix.Translation(-scalingCenter.X, -scalingCenter.Y, 0.0f);
                scaleMatrix = fromCenter * scalingRotationMatrix * toCenter * scaleMatrix;
            }

            var rotationMatrix = Matrix.RotationZ(rotation);
            var toRotationCenter = Matrix.Translation(rotationCenter.X, rotationCenter.Y, 0.0f);
            var fromRotationCenter = Matrix.Translation(-rotationCenter.X, -rotationCenter.Y, 0.0f);
            rotationMatrix = fromRotationCenter * rotationMatrix * toRotationCenter;
            var translationMatrix = Matrix.Translation(translation.X, translation.Y, 0.0f);
            return scaleMatrix * rotationMatrix * translationMatrix;
        }

        public static Matrix PerspectiveFovLH(float fov, float aspect, float znear, float zfar) {
            var yScale = 1.0f / (float)Math.Tan(fov / 2.0f);
            var xScale = yScale / aspect;
            var zRange = zfar - znear;
            return new Matrix {
                M11 = xScale,
                M12 = 0.0f,
                M13 = 0.0f,
                M14 = 0.0f,
                M21 = 0.0f,
                M22 = yScale,
                M23 = 0.0f,
                M24 = 0.0f,
                M31 = 0.0f,
                M32 = 0.0f,
                M33 = zfar / zRange,
                M34 = 1.0f,
                M41 = 0.0f,
                M42 = 0.0f,
                M43 = -(znear * zfar) / zRange,
                M44 = 0.0f
            };
        }

        public static Matrix PerspectiveFovRH(float fov, float aspect, float znear, float zfar) {
            var yScale = 1.0f / (float)Math.Tan(fov / 2.0f);
            var xScale = yScale / aspect;
            var zRange = znear - zfar;
            return new Matrix {
                M11 = xScale,
                M12 = 0.0f,
                M13 = 0.0f,
                M14 = 0.0f,
                M21 = 0.0f,
                M22 = yScale,
                M23 = 0.0f,
                M24 = 0.0f,
                M31 = 0.0f,
                M32 = 0.0f,
                M33 = (zfar + znear) / zRange,
                M34 = -1.0f,
                M41 = 0.0f,
                M42 = 0.0f,
                M43 = (2 * zfar * znear) / zRange,
                M44 = 0.0f
            };
        }

        public static bool Intersects_v2(this Plane plane, Vector3 start, Vector3 end, out Vector3 intersectPoint) {
            var direction = end - start;
            var denom = Vector3.Dot(plane.Normal, direction);
            if (Math.Abs(denom) < 1e-6f) {
                intersectPoint = default;
                return false;
            }

            var numerator = -(Vector3.Dot(plane.Normal, start) + plane.D);
            var t = numerator / denom;
            if (t < 0.0f || t > 1.0f) {
                intersectPoint = default;
                return false;
            }

            intersectPoint = start + t * direction;
            return true;
        }
    }
}