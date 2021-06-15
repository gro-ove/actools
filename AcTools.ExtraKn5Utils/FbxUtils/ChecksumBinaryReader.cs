// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.IO;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    public class ChecksumBinaryReader : BinaryReader {
        private const int modAdler = 65521;
        private uint checksumA = 1;
        private uint checksumB = 0;

        public uint Checksum {
            get {
                checksumA %= modAdler;
                checksumB %= modAdler;
                return ((checksumB << 16) | checksumA);
            }
        }

        public ChecksumBinaryReader(Stream stream) : base(stream) { }

        private void UpdateChecksum(byte[] array) {
            foreach (var c in array) {
                checksumA = (checksumA + c) % modAdler;
                checksumB = (checksumB + checksumA) % modAdler;
            }
        }

        public override byte ReadByte() {
            var value = base.ReadByte();
            UpdateChecksum(BitConverter.GetBytes(value));
            return value;
        }

        public override int ReadInt32() {
            var value = base.ReadInt32();
            UpdateChecksum(BitConverter.GetBytes(value));
            return value;
        }

        public override long ReadInt64() {
            var value = base.ReadInt64();
            UpdateChecksum(BitConverter.GetBytes(value));
            return value;
        }

        public override float ReadSingle() {
            var value = base.ReadSingle();
            UpdateChecksum(BitConverter.GetBytes(value));
            return value;
        }

        public override double ReadDouble() {
            var value = base.ReadDouble();
            UpdateChecksum(BitConverter.GetBytes(value));
            return value;
        }

        public override bool ReadBoolean() {
            var value = base.ReadBoolean();
            UpdateChecksum(BitConverter.GetBytes(value));
            return value;
        }
    }
}