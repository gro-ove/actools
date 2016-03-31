using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Kn5Render.Kn5Render {
    public static class SlimDxExtend {
        public static void DrawAllPasses(this EffectTechnique tech, DeviceContext context, int indexCount) {
            for (var i = 0; i < tech.Description.PassCount; i++) {
                tech.GetPassByIndex(i).Apply(context);
                context.DrawIndexed(indexCount, 0, 0);
            }
        }

        private static readonly Dictionary<int, CacheObject> Cache = new Dictionary<int, CacheObject>(); 

        private class CacheObject : IDisposable {
            public static byte[] Array = new byte[256];
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

        public static void Set(this EffectVariable variable, ShaderMaterial o) {
            Set(variable, o, ShaderMaterial.Stride);
        }

        public static void Set(this EffectVariable variable, DirectionalLight o) {
            Set(variable, o, DirectionalLight.Stride);
        }

        public static void Set(this EffectVariable variable, object o) {
            Set(variable, o, Marshal.SizeOf(o));
        }

        public static BoundingBox Expand(this BoundingBox a, BoundingBox b) {
            if (a.Minimum.X > b.Minimum.X) a.Minimum.X = b.Minimum.X;
            if (a.Minimum.Y > b.Minimum.Y) a.Minimum.Y = b.Minimum.Y;
            if (a.Minimum.Z > b.Minimum.Z) a.Minimum.Z = b.Minimum.Z;
            if (a.Maximum.X < b.Maximum.X) a.Maximum.X = b.Maximum.X;
            if (a.Maximum.Y < b.Maximum.Y) a.Maximum.Y = b.Maximum.Y;
            if (a.Maximum.Z < b.Maximum.Z) a.Maximum.Z = b.Maximum.Z;
            return a;
        }

        public static Vector3 GetCenter(this BoundingBox a) {
            return (a.Maximum + a.Minimum) / 2.0f;
        }

        public static Vector3 GetSize(this BoundingBox a) {
            return a.Maximum - a.Minimum;
        }

        public static float GetSizeX(this BoundingBox a) {
            return a.Maximum.X - a.Minimum.X;
        }

        public static float GetSizeY(this BoundingBox a) {
            return a.Maximum.Y - a.Minimum.Y;
        }

        public static float GetSizeZ(this BoundingBox a) {
            return a.Maximum.Z - a.Minimum.Z;
        }
    }
}
