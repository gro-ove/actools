using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Debug = System.Diagnostics.Debug;
using Device = SlimDX.Direct3D11.Device;

namespace AcTools.Render.Base.Utils {
    public static class SlimDxExtension {
        public static BoundingBox ToBoundingBox(this IEnumerable<Vector3> vertices) {
            using (var ie = vertices.GetEnumerator()) {
                if (!ie.MoveNext()) return new BoundingBox();

                var b = new BoundingBox(ie.Current, ie.Current);
                while (ie.MoveNext()) {
                    ie.Current.ExtendBoundingBox(ref b);
                }

                return b;
            }
        }

        public static BoundingBox Grow(this BoundingBox b, Vector3 v) {
            return new BoundingBox(b.Minimum - v, b.Maximum + v);
        }

        public static void ExtendBoundingBox(this Vector3 v, ref BoundingBox bb) {
            if (bb.Maximum.X < v.X) bb.Maximum.X = v.X;
            if (bb.Maximum.Y < v.Y) bb.Maximum.Y = v.Y;
            if (bb.Maximum.Z < v.Z) bb.Maximum.Z = v.Z;
            if (bb.Minimum.X > v.X) bb.Minimum.X = v.X;
            if (bb.Minimum.Y > v.Y) bb.Minimum.Y = v.Y;
            if (bb.Minimum.Z > v.Z) bb.Minimum.Z = v.Z;
        }

        public static void ExtendBoundingBox(this BoundingBox b, ref BoundingBox bb) {
            if (bb.Maximum.X < b.Maximum.X) bb.Maximum.X = b.Maximum.X;
            if (bb.Maximum.Y < b.Maximum.Y) bb.Maximum.Y = b.Maximum.Y;
            if (bb.Maximum.Z < b.Maximum.Z) bb.Maximum.Z = b.Maximum.Z;
            if (bb.Minimum.X > b.Minimum.X) bb.Minimum.X = b.Minimum.X;
            if (bb.Minimum.Y > b.Minimum.Y) bb.Minimum.Y = b.Minimum.Y;
            if (bb.Minimum.Z > b.Minimum.Z) bb.Minimum.Z = b.Minimum.Z;
        }

        public static void Extend(ref BoundingBox bb, ref Vector3 v) {
            if (bb.Maximum.X < v.X) bb.Maximum.X = v.X;
            if (bb.Maximum.Y < v.Y) bb.Maximum.Y = v.Y;
            if (bb.Maximum.Z < v.Z) bb.Maximum.Z = v.Z;
            if (bb.Minimum.X > v.X) bb.Minimum.X = v.X;
            if (bb.Minimum.Y > v.Y) bb.Minimum.Y = v.Y;
            if (bb.Minimum.Z > v.Z) bb.Minimum.Z = v.Z;
        }

        [Pure]
        public static BoundingBox ExtendBy(this BoundingBox bb, BoundingBox next) {
            return new BoundingBox(
                    new Vector3(
                            Math.Min(bb.Minimum.X, next.Minimum.X),
                            Math.Min(bb.Minimum.Y, next.Minimum.Y),
                            Math.Min(bb.Minimum.Z, next.Minimum.Z)),
                    new Vector3(
                            Math.Max(bb.Maximum.X, next.Maximum.X),
                            Math.Max(bb.Maximum.Y, next.Maximum.Y),
                            Math.Max(bb.Maximum.Z, next.Maximum.Z)));
        }

        public static Vector3 GetCenter(this BoundingBox bb) {
            return (bb.Minimum + bb.Maximum) / 2f;
        }

        public static Vector3 GetSize(this BoundingBox bb) {
            return bb.Maximum - bb.Minimum;
        }

        public static float GetVolume(this BoundingBox bb) {
            return (bb.Maximum - bb.Minimum).GetVolume();
        }

        public static float GetVolume(this Vector3 v) {
            return v.X * v.Y * v.Z;
        }

        public static Vector3 GetVector3(this Vector4 vec) {
            return new Vector3(vec.X, vec.Y, vec.Z);
        }

        public static float[] ToArray(this Vector2 vec) {
            return new []{ vec.X, vec.Y };
        }

        public static float[] ToArray(this Vector3 vec) {
            return new []{ vec.X, vec.Y, vec.Z };
        }

        public static Color ToDrawingColor(this Vector3 color) {
            return Color.FromArgb((int)(color.X * 255f).Clamp(0, 255), (int)(color.Y * 255f).Clamp(0, 255), (int)(color.Z * 255f).Clamp(0, 255));
        }

        public static Color ToDrawingColor(this Vector4 color) {
            return Color.FromArgb((int)(color.W * 255f).Clamp(0, 255), (int)(color.X * 255f).Clamp(0, 255), (int)(color.Y * 255f).Clamp(0, 255),
                    (int)(color.Z * 255f).Clamp(0, 255));
        }

        public static Vector3 ToVector3(this Color color) {
            return new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        }

        public static Vector4 ToVector4(this Color color) {
            return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

        public static float GetBrightness(this Vector3 vec) {
            return vec.X * 0.299f + vec.Y * 0.587f + vec.Z * 0.114f;
        }

        public static Vector3 FlipX(this Vector3 vec) {
            vec.X *= -1f;
            return vec;
        }

        public static Vector4 ToVector4(this Vector3 vec) {
            return new Vector4(vec, 0f);
        }

        public static BoundingBox Transform(this BoundingBox bb, Matrix matrix) {
            var a = Vector3.Transform(bb.Minimum, matrix);
            var b = Vector3.Transform(bb.Maximum, matrix);
            return new BoundingBox(
                    new Vector3(
                            Math.Min(a.X, b.X),
                            Math.Min(a.Y, b.Y),
                            Math.Min(a.Z, b.Z)),
                    new Vector3(
                            Math.Max(a.X, b.X),
                            Math.Max(a.Y, b.Y),
                            Math.Max(a.Z, b.Z)));
        }

        public static void DrawAllPasses(this EffectTechnique tech, DeviceContext context, int indexCount) {
            for (var i = 0; i < tech.Description.PassCount; i++) {
                tech.GetPassByIndex(i).Apply(context);
                context.DrawIndexed(indexCount, 0, 0);
            }
        }

        public static float[] ToArray(this Quaternion q) {
            return new[] { q.X, q.Y, q.Z, q.W };
        }

        public static Quaternion ToQuaternion(this float[] quaternion4) {
            return new Quaternion(quaternion4[0], quaternion4[1], quaternion4[2], quaternion4[3]);
        }

        public static Vector2 ToVector2(this float[] vec2) {
            return new Vector2(vec2[0], vec2[1]);
        }

        public static Vector3 ToVector3(this float[] vec3) {
            return new Vector3(vec3[0], vec3[1], vec3[2]);
        }

        public static Vector4 ToVector4(this float[] vec3) {
            return new Vector4(vec3[0], vec3[1], vec3[2], vec3[3]);
        }

        public static Vector3 ToVector3FixX(this float[] vec3) {
            return new Vector3(-vec3[0], vec3[1], vec3[2]);
        }

        public static Vector3 ToVector3Tangent(this float[] vec3) {
            return new Vector3(-vec3[0], vec3[1], vec3[2]);
        }

        public static Vector3 GetTranslationVector(this Matrix matrix) {
            return new Vector3(matrix.M41, matrix.M42, matrix.M43);
        }

        // ReSharper disable once InconsistentNaming
        public static Matrix ToMatrix(this float[] mat4x4) {
            return new Matrix {
                M11 = mat4x4[0],
                M12 = mat4x4[1],
                M13 = mat4x4[2],
                M14 = mat4x4[3],
                M21 = mat4x4[4],
                M22 = mat4x4[5],
                M23 = mat4x4[6],
                M24 = mat4x4[7],
                M31 = mat4x4[8],
                M32 = mat4x4[9],
                M33 = mat4x4[10],
                M34 = mat4x4[11],
                M41 = mat4x4[12],
                M42 = mat4x4[13],
                M43 = mat4x4[14],
                M44 = mat4x4[15]
            };
        }

        public static Matrix LookAtMatrixConsiderUp(this Vector3 o, Vector3 p, Vector3 u) {
            var d = Vector3.Normalize(p - o);
            u.Normalize();
            var s = Vector3.Normalize(Vector3.Cross(d, u));
            return ToMatrix(
                    d.X, d.Y, d.Z, 0,
                    u.X, u.Y, u.Z, 0,
                    s.X, s.Y, s.Z, 0,
                    o.X, o.Y, o.Z, 1);
        }

        public static Matrix LookAtMatrixXAxis(this Vector3 o, Vector3 p, Vector3 u) {
            var d = Vector3.Normalize(p - o);
            var s = Vector3.Normalize(Vector3.Cross(d, Vector3.Normalize(u)));
            var v = Vector3.Cross(s, d);
            return ToMatrix(
                    d.X, d.Y, d.Z, 0,
                    v.X, v.Y, v.Z, 0,
                    s.X, s.Y, s.Z, 0,
                    o.X, o.Y, o.Z, 1);
        }

        public static Matrix LookAtMatrix(this Vector3 o, Vector3 p, Vector3 u) {
            var d = Vector3.Normalize(o - p);
            var s = Vector3.Normalize(Vector3.Cross(Vector3.Normalize(u), d));
            var v = Vector3.Normalize(Vector3.Cross(d, s));
            return ToMatrix(
                    v.X, v.Y, v.Z, 0,
                    d.X, d.Y, d.Z, 0,
                    s.X, s.Y, s.Z, 0,
                    o.X, o.Y, o.Z, 1);
        }

        public static Matrix ToMatrix(float m11, float m12, float m13, float m14, float m21, float m22, float m23, float m24, float m31, float m32, float m33,
                float m34, float m41, float m42, float m43, float m44) {
            return new Matrix {
                M11 = m11,
                M12 = m12,
                M13 = m13,
                M14 = m14,
                M21 = m21,
                M22 = m22,
                M23 = m23,
                M24 = m24,
                M31 = m31,
                M32 = m32,
                M33 = m33,
                M34 = m34,
                M41 = m41,
                M42 = m42,
                M43 = m43,
                M44 = m44
            };
        }

        public static Matrix ToMatrixFixX(this float[] mat4x4) {
            var matrix = mat4x4.ToMatrix();

            Vector3 translation, scale;
            Quaternion rotation;
            matrix.Decompose(out scale, out rotation, out translation);
            translation.X *= -1;

            var axis = rotation.Axis;
            var angle = rotation.Angle;

            if (angle.Abs() < 0.0001f) {
                return Matrix.Scaling(scale) * Matrix.Translation(translation);
            }

            axis.Y *= -1;
            axis.Z *= -1;
            rotation = Quaternion.RotationAxis(axis, angle);

            var result = Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
            if (float.IsNaN(result[0, 0])) {
                AcToolsLogging.Write("CAN’T FIX MATRIX! PLEASE, SEND THE MODEL TO THE DEVELOPER");
                return matrix;
            }

            return result;
        }

        public static ushort[] ToIndicesFixX(this ushort[] indices) {
            var result = new ushort[indices.Length];
            for (var i = 0; i < indices.Length; i += 3) {
                result[i] = indices[i];
                result[i + 1] = indices[i + 2];
                result[i + 2] = indices[i + 1];
            }
            return result;
        }

        private static readonly Dictionary<int, CacheObject> Cache = new Dictionary<int, CacheObject>();
        private static readonly Dictionary<Tuple<int, int>, ArrayCacheObject> ArrayCache = new Dictionary<Tuple<int, int>, ArrayCacheObject>();

        private class CacheObject : IDisposable {
            public static byte[] Array = new byte[512];
            public readonly IntPtr Pointer;
            public readonly DataStream Data;

            public CacheObject(int len) {
                if (len > Array.Length) {
                    Array = new byte[len];
                } else {
                    System.Array.Clear(Array, 0, Array.Length);
                }

                Pointer = Marshal.AllocHGlobal(len);
                Data = new DataStream(Array, false, false);
            }

            public void Dispose() {
                Marshal.FreeHGlobal(Pointer);
                Data.Dispose();
            }
        }

        private class ArrayCacheObject : IDisposable {
            public static byte[] Array = new byte[5120];
            public readonly IntPtr Pointer;
            public readonly DataStream Data;

            public ArrayCacheObject(int elementLen, int arrayLen) {
                var len = arrayLen * elementLen;
                if (len > Array.Length) {
                    Array = new byte[len];
                } else {
                    System.Array.Clear(Array, 0, Array.Length);
                }

                Pointer = Marshal.AllocHGlobal(elementLen);
                Data = new DataStream(Array, false, false);
            }

            public void Dispose() {
                Marshal.FreeHGlobal(Pointer);
                Data.Dispose();
            }
        }

        public static void Set_(EffectVariable variable, object o, int len) {
            var arr = new byte[len];
            var ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(o, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            variable.SetRawValue(new DataStream(arr, false, false), len);
        }

        public static void SetObject(EffectVariable variable, object o, int len) {
            if (o == null) {
                // TODO (?)
            } else {
                CacheObject cobj;
                if (!Cache.TryGetValue(len, out cobj)) {
                    cobj = new CacheObject(len);
                    Cache[len] = cobj;
                    Debug.WriteLine("CACHED MEMORY AREA CREATED: " + len);
                }

                Marshal.StructureToPtr(o, cobj.Pointer, true);
                Marshal.Copy(cobj.Pointer, CacheObject.Array, 0, len);
                variable.SetRawValue(cobj.Data, len);
            }
        }

        public static void SetArray(EffectVariable variable, Array o, int elementLen) {
            if (o == null) {
                // TODO (?)
            } else {
                var len = elementLen * o.Length;
                var key = Tuple.Create(elementLen, o.Length);

                ArrayCacheObject cobj;
                if (!ArrayCache.TryGetValue(key, out cobj)) {
                    cobj = new ArrayCacheObject(elementLen, o.Length);
                    ArrayCache[key] = cobj;
                    Debug.WriteLine("CACHED MEMORY AREA CREATED: " + len);
                }

                for (var i = 0; i < o.Length; i++) {
                    Marshal.StructureToPtr(o.GetValue(i), cobj.Pointer, true);
                    Marshal.Copy(cobj.Pointer, ArrayCacheObject.Array, elementLen * i, elementLen);
                }

                variable.SetRawValue(cobj.Data, len);
            }
        }

        public static void SetObject(this EffectVariable variable, object o) {
            SetObject(variable, o, Marshal.SizeOf(o));
        }

        public static BlendState CreateBlendState(this Device device, RenderTargetBlendDescription description) {
            var desc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };
            desc.RenderTargets[0] = description;
            return BlendState.FromDescription(device, desc);
        }

        public static long GetBitsPerPixel(this Format format) {
            switch (format) {
                case Format.Unknown:
                    return 0;
                case Format.R32G32B32A32_Typeless:
                case Format.R32G32B32A32_Float:
                case Format.R32G32B32A32_UInt:
                case Format.R32G32B32A32_SInt:
                    return 128;
                case Format.R32G32B32_Typeless:
                case Format.R32G32B32_Float:
                case Format.R32G32B32_UInt:
                case Format.R32G32B32_SInt:
                    return 96;
                case Format.R16G16B16A16_Typeless:
                case Format.R16G16B16A16_Float:
                case Format.R16G16B16A16_UNorm:
                case Format.R16G16B16A16_UInt:
                case Format.R16G16B16A16_SNorm:
                case Format.R16G16B16A16_SInt:
                    return 64;
                case Format.R32G32_Typeless:
                case Format.R32G32_Float:
                case Format.R32G32_UInt:
                case Format.R32G32_SInt:
                case Format.R32G8X24_Typeless:
                    return 64;
                case Format.D32_Float_S8X24_UInt:
                case Format.R32_Float_X8X24_Typeless:
                case Format.X32_Typeless_G8X24_UInt:
                case Format.R10G10B10A2_Typeless:
                case Format.R10G10B10A2_UNorm:
                case Format.R10G10B10A2_UInt:
                case Format.R11G11B10_Float:
                case Format.R8G8B8A8_Typeless:
                case Format.R8G8B8A8_UNorm:
                case Format.R8G8B8A8_UNorm_SRGB:
                case Format.R8G8B8A8_UInt:
                case Format.R8G8B8A8_SNorm:
                case Format.R8G8B8A8_SInt:
                case Format.R16G16_Typeless:
                case Format.R16G16_Float:
                case Format.R16G16_UNorm:
                case Format.R16G16_UInt:
                case Format.R16G16_SNorm:
                case Format.R16G16_SInt:
                case Format.R32_Typeless:
                case Format.D32_Float:
                case Format.R32_Float:
                case Format.R32_UInt:
                case Format.R32_SInt:
                case Format.R24G8_Typeless:
                case Format.D24_UNorm_S8_UInt:
                case Format.R24_UNorm_X8_Typeless:
                case Format.X24_Typeless_G8_UInt:
                case Format.R9G9B9E5_SharedExp:
                case Format.R8G8_B8G8_UNorm:
                case Format.G8R8_G8B8_UNorm:
                case Format.B8G8R8A8_UNorm:
                case Format.B8G8R8X8_UNorm:
                case Format.R10G10B10_XR_Bias_A2_UNorm:
                case Format.B8G8R8A8_Typeless:
                case Format.B8G8R8A8_UNorm_SRGB:
                case Format.B8G8R8X8_Typeless:
                case Format.B8G8R8X8_UNorm_SRGB:
                    return 32;
                case Format.R8G8_Typeless:
                case Format.R8G8_UNorm:
                case Format.R8G8_UInt:
                case Format.R8G8_SNorm:
                case Format.R8G8_SInt:
                case Format.R16_Typeless:
                case Format.R16_Float:
                case Format.D16_UNorm:
                case Format.R16_UNorm:
                case Format.R16_UInt:
                case Format.R16_SNorm:
                case Format.R16_SInt:
                case Format.B5G6R5_UNorm:
                case Format.B5G5R5A1_UNorm:
                    return 16;
                case Format.R8_Typeless:
                case Format.R8_UNorm:
                case Format.R8_UInt:
                case Format.R8_SNorm:
                case Format.R8_SInt:
                case Format.A8_UNorm:
                    return 8;
                case Format.R1_UNorm:
                    return 1;
                case Format.BC1_Typeless:
                case Format.BC1_UNorm:
                case Format.BC1_UNorm_SRGB:
                case Format.BC2_Typeless:
                case Format.BC2_UNorm:
                case Format.BC2_UNorm_SRGB:
                case Format.BC3_Typeless:
                case Format.BC3_UNorm:
                case Format.BC3_UNorm_SRGB:
                case Format.BC4_Typeless:
                case Format.BC4_UNorm:
                case Format.BC4_SNorm:
                case Format.BC5_Typeless:
                case Format.BC5_UNorm:
                case Format.BC5_SNorm:
                case Format.BC6_Typeless:
                case Format.BC6_UFloat16:
                case Format.BC6_SFloat16:
                case Format.BC7_Typeless:
                case Format.BC7_UNorm:
                case Format.BC7_UNorm_SRGB:
                    throw new NotImplementedException($"Type {format} is unknown to find size of");
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        public static long GetBytesSize([NotNull] this ShaderResourceView view) {
            var n = (view.Resource as Texture2D)?.Description;
            if (!n.HasValue) return 0;

            var d = n.Value;
            return d.Width * d.Height * d.Format.GetBitsPerPixel() / 8;
        }

        // Temporary
        internal static string ToReadableSize(this long i) {
            var a = i < 0 ? -i : i;

            string suffix;
            double readable;
            if (a >= 0x40000000) {
                suffix = "GB";
                readable = i >> 20;
            } else if (a >= 0x100000) {
                suffix = "MB";
                readable = i >> 10;
            } else if (a >= 0x400) {
                suffix = "KB";
                readable = i;
            } else {
                return i.ToString(@"0 " + "B");
            }

            readable = readable / 1024;

            int round;
            if (readable < 10) {
                round = 2;
            } else if (readable < 100) {
                round = 1;
            } else {
                round = 0;
            }

            return $@"{readable.ToString($@"F{round}")} {suffix}";
        }
    }
}
