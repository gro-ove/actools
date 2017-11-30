using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Reflections {
    public class LightProbe : ReflectionCubemap {
        public LightProbe() : base(32) { }

        protected override int GetMipLevels(int resolution, int minResolution) {
            return 1;
        }

        private float[][] _buffer = { new float[9], new float[9], new float[9] };
        private Vector4[] _values = new Vector4[9];

        public Vector4[] Values => _values;

        protected override void OnCubemapUpdate(DeviceContextHolder holder) {
            base.OnCubemapUpdate(holder);

            var buffer = _buffer;
            ShProjectCubeMap(holder.DeviceContext, 3, CubeTex, buffer[0], buffer[1], buffer[2]);
            for (var i = 0; i < 9; i++) {
                _values[i].X = buffer[0][i];
                _values[i].Y = buffer[1][i];
                _values[i].Z = buffer[2][i];
            }
        }

        private static ConstructorInfo _resultConstructorInfo;

        private static Result ToResult(int value) {
            if (value == 0) return default(Result);
            return (Result)(_resultConstructorInfo ?? (_resultConstructorInfo =
                    AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName.StartsWith("SlimDX") && new AssemblyName(a.FullName).Name == "SlimDX")
                             .GetType("SlimDX.Result").GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0]))
                    .Invoke(new object[] { value });
        }

        private static void ShProjectCubeMap(DeviceContext contextRef, int order, Texture2D cubeMapRef, float[] rOutRef, float[] gOutRef, float[] bOutRef) {
            unsafe {
                fixed (void* r = rOutRef)
                fixed (void* g = gOutRef)
                fixed (void* b = bOutRef) {
                    var result = ToResult(D3DX11SHProjectCubeMap(
                            (void*)(contextRef?.ComPointer ?? IntPtr.Zero), order,
                            (void*)(cubeMapRef?.ComPointer ?? IntPtr.Zero), r, g, b));
                    if (result != default(Result)) {
                        throw new SlimDXException(result);
                    }
                }
            }
        }

        [DllImport("d3dx11_43.dll", EntryPoint = "D3DX11SHProjectCubeMap", CallingConvention = CallingConvention.StdCall)]
        private static extern unsafe int D3DX11SHProjectCubeMap(void* arg0, int arg1, void* arg2, void* arg3, void* arg4, void* arg5);
    }
}