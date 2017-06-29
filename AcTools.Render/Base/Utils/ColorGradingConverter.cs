using System;
using System.Drawing;
using System.Drawing.Imaging;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;

namespace AcTools.Render.Base.Utils {
    public static class ColorGradingConverter {
        private static void CopySafe(Bitmap bitmap, int size, DataStream data){
            var u = new byte[]{ 0, 0, 0, 255 };
            for (int w = size * size, y = 0; y < size; y++) {
                for (var x = 0; x < w; x++) {
                    var c = bitmap.GetPixel(x, y);
                    u[0] = c.R; u[1] = c.B; u[2] = c.G;
                    data.Write(u, 0, 4);
                }
            }
        }

        private static unsafe void Copy24Rgb(Bitmap bitmap, int size, DataStream data){
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, size * size, size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var ptr = (byte*)bmpData.Scan0.ToPointer();

            var q = size * size * size * 3;
            var u = new byte[4];
            u[3] = 255;
            for (var i = 0; i < q; i += 3){
                u[1] = ptr[i];
                u[2] = ptr[i + 1];
                u[0] = ptr[i + 2];
                data.Write(u, 0, 4);
            }

            bitmap.UnlockBits(bmpData);
        }

        private static unsafe void Copy32Rgb(Bitmap bitmap, int size, DataStream data){
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, size * size, size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var ptr = (byte*)bmpData.Scan0.ToPointer();

            var q = size * size * size * 4;
            var u = new byte[4];
            for (var i = 0; i < q; i += 4){
                u[1] = ptr[i];
                u[2] = ptr[i + 1];
                u[0] = ptr[i + 2];
                u[3] = ptr[i + 3];
                data.Write(u, 0, 4);
            }

            bitmap.UnlockBits(bmpData);
        }

        public static Texture3D CreateTexture(Device device, Bitmap bitmap) {
            if (bitmap.Width != bitmap.Height * bitmap.Height) {
                throw new Exception("Width should be equal to height squared");
            }

            var texture = new Texture3D(device, new Texture3DDescription {
                Width = bitmap.Height,
                Height = bitmap.Height,
                Depth = bitmap.Height,
                MipLevels = 1,
                Format = Format.R8G8B8A8_UNorm,
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write
            });

            var rect = device.ImmediateContext.MapSubresource(texture, 0, MapMode.WriteDiscard, SlimDX.Direct3D11.MapFlags.None);
            if (rect.Data.CanWrite) {
                switch (bitmap.PixelFormat){
                    case PixelFormat.Format24bppRgb:
                        Copy24Rgb(bitmap, bitmap.Height, rect.Data);
                        break;
                    case PixelFormat.Format32bppArgb:
                        Copy32Rgb(bitmap, bitmap.Height, rect.Data);
                        break;
                    default:
                        AcToolsLogging.Write("Not supported for fast conversion: " + bitmap.PixelFormat);
                        CopySafe(bitmap, bitmap.Height, rect.Data);
                        break;
                }
            }

            device.ImmediateContext.UnmapSubresource(texture, 0);
            return texture;
        }

    }
}