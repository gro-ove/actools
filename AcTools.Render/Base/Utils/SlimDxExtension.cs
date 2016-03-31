using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Utils {
    public static class SlimDxExtension {
        public static void Dispose<T>(ref T obj) where T : class, IDisposable {
            if (obj == null) return;
            obj.Dispose();
            obj = null;
        }

        public static void DrawAllPasses(this EffectTechnique tech, DeviceContext context, int indexCount) {
            for (var i = 0; i < tech.Description.PassCount; i++) {
                tech.GetPassByIndex(i).Apply(context);
                context.DrawIndexed(indexCount, 0, 0);
            }
        }

        public static Vector2 ToVector2(this float[] vec2) {
            return new Vector2(vec2[0], vec2[1]);
        }

        public static Vector3 ToVector3(this float[] vec3) {
            return new Vector3(vec3[0], vec3[1], vec3[2]);
        }

        public static Vector3 ToVector3FixX(this float[] vec3) {
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

        public static Matrix ToMatrixFixX(this float[] mat4x4) {
            var matrix = mat4x4.ToMatrix();

            Vector3 translation, scale;
            Quaternion rotation;
            matrix.Decompose(out scale, out rotation, out translation);
            translation.X *= -1;
            var axis = rotation.Axis;
            axis.Y *= -1;
            axis.Z *= -1;
            rotation = Quaternion.RotationAxis(axis, rotation.Angle);
            return Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
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
            }
        }

        public static void SetOld(EffectVariable variable, object o, int len) {
            var arr = new byte[len];
            var ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(o, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            variable.SetRawValue(new DataStream(arr, false, false), len);
        }

        public static void Set(EffectVariable variable, object o, int len) {
            if (o == null) {
                // TODO (?)
            } else {
                var cobj = Cache.ContainsKey(len) ? Cache[len] : (Cache[len] = new CacheObject(len));
                Marshal.StructureToPtr(o, cobj.Pointer, true);
                Marshal.Copy(cobj.Pointer, CacheObject.Array, 0, len);
                variable.SetRawValue(cobj.Data, len);
            }
        }

        public static void Set(this EffectVariable variable, object o) {
            Set(variable, o, Marshal.SizeOf(o));
        }

        public static BlendState CreateBlendState(this Device device, BlendOperation operation) {
            var desc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            desc.RenderTargets[0].BlendEnable = true;
            desc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
            desc.RenderTargets[0].DestinationBlend = BlendOption.One;
            desc.RenderTargets[0].BlendOperation = operation;
            desc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
            desc.RenderTargets[0].DestinationBlendAlpha = BlendOption.Zero;
            desc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
            desc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

            return BlendState.FromDescription(device, desc);
        }
    }
}
