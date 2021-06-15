// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.IO;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    public class ChecksumBinaryWriter : BinaryWriter {
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

        public ChecksumBinaryWriter(Stream stream) : base(stream) { }

        private void UpdateChecksum(byte[] array) {
            foreach (var c in array) {
                checksumA = (checksumA + c) % modAdler;
                checksumB = (checksumB + checksumA) % modAdler;
            }
        }

        public override void Write(byte value) {
            UpdateChecksum(BitConverter.GetBytes(value));
            base.Write(value);
        }

        public override void Write(int value) {
            UpdateChecksum(BitConverter.GetBytes(value));
            base.Write(value);
        }

        public override void Write(long value) {
            UpdateChecksum(BitConverter.GetBytes(value));
            base.Write(value);
        }

        public override void Write(float value) {
            UpdateChecksum(BitConverter.GetBytes(value));
            base.Write(value);
        }

        public override void Write(double value) {
            UpdateChecksum(BitConverter.GetBytes(value));
            base.Write(value);
        }

        public override void Write(bool value) {
            UpdateChecksum(BitConverter.GetBytes(value));
            base.Write(value);
        }
    }
}