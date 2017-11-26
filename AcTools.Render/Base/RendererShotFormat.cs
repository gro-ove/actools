using System;
using System.IO;
using AcTools.Render.Utils;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base {
    public enum RendererShotFormat {
        Jpeg = 0,
        Png = 1,
        Dds = 2,
        Gif = 3,
        Bmp = 4,
        Tiff = 5,
        HdrDds = 1000,
        HdrExr = 1001
    }

    public static class RendererShotFormatExtension {
        public static string GetExtension(this RendererShotFormat format) {
            switch (format) {
                case RendererShotFormat.Jpeg:
                    return ".jpg";
                case RendererShotFormat.Png:
                    return ".png";
                case RendererShotFormat.Dds:
                case RendererShotFormat.HdrDds:
                    return ".dds";
                case RendererShotFormat.Gif:
                    return ".gif";
                case RendererShotFormat.Bmp:
                    return ".bmp";
                case RendererShotFormat.HdrExr:
                    return ".exr";
                case RendererShotFormat.Tiff:
                    return ".tiff";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        public static bool IsHdr(this RendererShotFormat format) {
            return format >= RendererShotFormat.HdrDds;
        }

        public static bool IsWindowsEncoderBroken(this RendererShotFormat format) {
            return format == RendererShotFormat.Jpeg;
        }

        public static void ConvertToStream(this RendererShotFormat format, DeviceContext deviceContext, Texture2D texture, Stream stream) {
            switch (format) {
                case RendererShotFormat.HdrExr:
                    OpenExrEncoder.ConvertToExr(deviceContext.Device, texture, stream);
                    break;
            }

            Texture2D.ToStream(deviceContext, texture, format.ToImageFileFormat(), stream);
        }

        public static ImageFileFormat ToImageFileFormat(this RendererShotFormat format) {
            switch (format) {
                case RendererShotFormat.Jpeg:
                    return ImageFileFormat.Jpg;
                case RendererShotFormat.Png:
                    return ImageFileFormat.Png;
                case RendererShotFormat.Dds:
                case RendererShotFormat.HdrDds:
                    return ImageFileFormat.Dds;
                case RendererShotFormat.Gif:
                    return ImageFileFormat.Gif;
                case RendererShotFormat.Bmp:
                    return ImageFileFormat.Bmp;
                case RendererShotFormat.Tiff:
                    return ImageFileFormat.Tiff;
                case RendererShotFormat.HdrExr:
                    return ImageFileFormat.Dds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
    }
}