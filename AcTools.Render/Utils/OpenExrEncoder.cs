using System;
using System.IO;
using System.Text;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Utils {
    public static class OpenExrEncoder {
        private const int ChannelCount = 4;
        private const int ValueSize = sizeof(float);

        public static void ConvertToExr(SlimDX.Direct3D11.Device device, Texture2D texture, Stream destination) {
            if (texture.Description.Format != Format.R32G32B32A32_Float) {
                throw new NotSupportedException("Not supported format: " + texture.Description.Format);
            }

            WriteHeader(destination, texture.Description.Width, texture.Description.Height);
            WriteHeaderOffsets(destination, texture.Description.Width, texture.Description.Height);
            WriteData(device, texture, destination);
        }

        private static void WriteHeader(Stream destination, int width, int height) {
            WriteInt(destination, 20000630);
            WriteByte(destination, 0x02, 0, 0, 0);

            void WriteChannel(string name, bool linear = false, int xSampling = 1, int ySampling = 1) {
                WriteString(destination, name);
                WriteInt(destination, ValueSize == 4 ? 2 : ValueSize == 2 ? 1 : 0);
                WriteByte(destination, (byte)(linear ? 1 : 0), 0, 0, 0);
                WriteInt(destination, xSampling, ySampling);
            }

            WriteString(destination, "channels", "chlist");
            WriteInt(destination, 18 * ChannelCount + 1); // attribute size
            WriteChannel("A");
            WriteChannel("B");
            WriteChannel("G");
            WriteChannel("R");
            WriteByte(destination, 0);

            WriteString(destination, "compression", "compression");
            WriteInt(destination, 1); // attribute size
            WriteByte(destination, (byte)0);

            WriteString(destination, "dataWindow", "box2i");
            WriteInt(destination, 16, 0, 0, width - 1, height - 1);

            WriteString(destination, "displayWindow", "box2i");
            WriteInt(destination, 16, 0, 0, width - 1, height - 1);

            WriteString(destination, "lineOrder", "lineOrder");
            WriteInt(destination, 1);
            WriteByte(destination, 0);

            WriteString(destination, "pixelAspectRatio", "float");
            WriteInt(destination, 4);
            Write(destination, 1f);

            WriteString(destination, "screenWindowCenter", "v2f");
            WriteInt(destination, 8, 0, 0);

            WriteString(destination, "screenWindowWidth", "float");
            WriteInt(destination, 4);
            Write(destination, 1f);

            WriteByte(destination, 0);
        }

        private static void WriteHeaderOffsets(Stream destination, int width, int height) {
            var offsetsStart = height * sizeof(long) + destination.Position;
            var scanlineExtraSize = ValueSize * ChannelCount * width + sizeof(int) * 2;
            for (var i = 0; i < height; i++) {
                Write(destination, offsetsStart + scanlineExtraSize * i);
            }
        }

        private static void WriteData(SlimDX.Direct3D11.Device device, Texture2D texture, Stream destination) {
            var width = texture.Description.Width;
            var height = texture.Description.Height;

            using (var copy = new Texture2D(device, new Texture2DDescription {
                SampleDescription = new SampleDescription(1, 0),
                Width = width,
                Height = height,
                ArraySize = texture.Description.ArraySize,
                MipLevels = texture.Description.MipLevels,
                Format = texture.Description.Format,
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read
            })) {
                device.ImmediateContext.CopyResource(texture, copy);

                try {
                    var dataBox = device.ImmediateContext.MapSubresource(copy, 0, MapMode.Read, SlimDX.Direct3D11.MapFlags.None);
                    var ddsData = dataBox.Data;
                    var ddsDataRowSize = dataBox.RowPitch;

                    // scanline offsets from the beginning of the file
                    var scanlineDataSize = ValueSize * ChannelCount * width;
                    var channelsBytes = new byte[scanlineDataSize];
                    var temporary = new byte[16];

                    for (var i = 0; i < height; i++) {
                        WriteInt(destination, i, scanlineDataSize);
                        CopyAll(ddsData, temporary, width, channelsBytes);
                        destination.Write(channelsBytes, 0, channelsBytes.Length);

                        var wentDownRow = ddsData.Position % ddsDataRowSize;
                        if (wentDownRow > 0) {
                            ddsData.Seek(ddsDataRowSize - wentDownRow, SeekOrigin.Current);
                        }
                    }
                } finally {
                    device.ImmediateContext.UnmapSubresource(texture, 0);
                }
            }
        }

        private static unsafe void CopyAll(Stream ddsData, byte[] temporary, int width, byte[] channelsBytes) {
            fixed (byte* cf = temporary, tf = channelsBytes) {
                uint* cl = (uint*)cf, tl = (uint*)tf;
                int offset2 = width * 2, offset3 = width * 3;
                for (var j = 0; j < width; j++) {
                    ddsData.Read(temporary, 0, 16);
                    tl[j] = cl[3];
                    tl[j + width] = cl[2];
                    tl[j + offset2] = cl[1];
                    tl[j + offset3] = cl[0];
                }
            }
        }

        private static void WriteString(Stream writer, params string[] s) {
            foreach (var p in s) {
                var b = Encoding.ASCII.GetBytes(p);
                writer.Write(b, 0, b.Length);
                writer.WriteByte(0);
            }
        }

        private static void WriteByte(Stream stream, params byte[] s) {
            stream.Write(s, 0, s.Length);
        }

        private static void Write(Stream stream, params float[] s) {
            for (var i = 0; i < s.Length; i++) {
                var p = s[i];
                stream.Write(BitConverter.GetBytes(p), 0, sizeof(float));
            }
        }

        private static void WriteInt(Stream stream, params int[] s) {
            for (var i = 0; i < s.Length; i++) {
                var p = s[i];
                stream.Write(BitConverter.GetBytes(p), 0, sizeof(int));
            }
        }

        private static void Write(Stream stream, params long[] s) {
            for (var i = 0; i < s.Length; i++) {
                var p = s[i];
                stream.Write(BitConverter.GetBytes(p), 0, sizeof(long));
            }
        }
    }
}