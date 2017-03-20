using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AcTools.Utils {
    public static class ExifComment {
        private sealed class ExifReader : ReadAheadBinaryReader {
            private Dictionary<ushort, long> _ifd0PrimaryCatalogue;

            private bool _isLittleEndian;
            private long _tiffHeaderStart;

            public ExifReader(string fileName) : base(fileName) {
                _isLittleEndian = false;

                if (ReadUShort() != 0xFFD8) throw new Exception("File is not a valid JPEG");

                try {
                    ReadToExifStart();
                } catch (Exception ex) {
                    throw new Exception("Unable to locate EXIF content", ex);
                }

                try {
                    CreateTagIndex();
                } catch (Exception ex) {
                    throw new Exception("Error indexing EXIF tags", ex);
                }
            }

            private ushort ToUShort(byte[] data) {
                if (_isLittleEndian != IsLittleEndian) Array.Reverse(data);
                return (ushort)ToInt16(data, 0);
            }

            private uint ToUInt(byte[] data) {
                if (_isLittleEndian != IsLittleEndian) Array.Reverse(data);
                return (uint)ToInt32(data, 0);
            }

            private ushort ReadUShort() {
                return ToUShort(ReadBytes(2));
            }

            private uint ReadUInt() {
                return ToUInt(ReadBytes(4));
            }

            private string ReadString(int chars) {
                var bytes = ReadBytes(chars);
                return Encoding.ASCII.GetString(bytes, 0, bytes.Length);
            }

            private byte[] ReadBytes(ushort tiffOffset, int byteCount) {
                var originalOffset = Position;
                Seek(tiffOffset + _tiffHeaderStart, SeekOrigin.Begin);
                var data = ReadBytes(byteCount);
                Position = originalOffset;
                return data;
            }

            private void ReadToExifStart() {
                byte markerStart;
                byte markerNumber = 0;
                while ((markerStart = ReadByte()) == 0xFF && (markerNumber = ReadByte()) != 0xE1) {
                    var dataLength = ReadUShort();
                    var offset = dataLength - 2;
                    Skip(offset);
                }

                if (markerStart != 0xFF || markerNumber != 0xE1) throw new Exception("Could not find EXIF data block");
            }

            private void CreateTagIndex() {
                ReadUShort();
                if (ReadString(4) != "Exif") throw new Exception("EXIF data not found");
                if (ReadUShort() != 0) throw new Exception("Malformed EXIF data");

                _tiffHeaderStart = Position;
                _isLittleEndian = ReadString(2) == "II";
                if (ReadUShort() != 0x002A) throw new Exception("Error in TIFF data");

                var ifdOffset = ReadUInt();
                Position = ifdOffset + _tiffHeaderStart;
                _ifd0PrimaryCatalogue = CatalogueIfd();
            }

            public byte[] GetTagValue(ushort tagId) {
                return GetTagBytes(_ifd0PrimaryCatalogue, tagId);
            }

            private byte[] GetTagBytes(Dictionary<ushort, long> tagDictionary, ushort tagId) {
                if (!tagDictionary.ContainsKey(tagId)) return null;

                var tagOffset = tagDictionary[tagId];
                Position = tagOffset;

                var actualTagId = ReadUShort();
                var tagType = ReadUShort();

                if (actualTagId != tagId || tagType != 1) throw new NotSupportedException();
                var numberOfComponents = ReadUInt();
                var tagData = ReadBytes(4);

                var dataSize = (int)numberOfComponents;
                if (dataSize > 4) {
                    var offsetAddress = ToUShort(tagData);
                    return ReadBytes(offsetAddress, dataSize);
                }

                Array.Resize(ref tagData, dataSize);
                return tagData;
            }

            private Dictionary<ushort, long> CatalogueIfd() {
                var entryCount = ReadUShort();
                var tagOffsets = new Dictionary<ushort, long>(entryCount);
                for (var i = 0; i < entryCount; i++) {
                    var tagNumber = ReadUShort();
                    tagOffsets[tagNumber] = Position - 2;
                    Skip(10);
                }

                return tagOffsets;
            }
        }

        public static string Read(string filename) {
            try {
                using (var reader = new ExifReader(filename)) {
                    // XPComment = 0x9c9c
                    var b = reader.GetTagValue(0x9c9c);
                    return b == null ? null : Encoding.Unicode.GetString(b);
                }
            } catch (Exception e) when (e.Message == "Unable to locate EXIF content") {
                return null;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }
    }
}