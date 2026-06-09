using System;
using System.IO;
using JetBrains.Annotations;

namespace AcTools.AcdFile {
    internal sealed class AcdReader : ReadAheadBinaryReader {
        [NotNull]
        private readonly IAcdEncryption _enc;

        public AcdReader(string filename) : this(filename, File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {}

        public AcdReader(string filename, Stream input) : base(input) {
            _enc = AcdEncryption.FromAcdFilename(filename);

            if (ReadInt32() == -1111){
                ReadInt32();
            } else {
                BaseStream.Seek(0, SeekOrigin.Begin);
            }
        }

        private byte[] ReadData() {
            var length = ReadInt32();

            var result = new byte[length];
            for (var i = 0; i < length; i++) {
                result[i] = ReadByte();
                Skip(3);
            }

            _enc.Decrypt(result);
            return result;
        }

        private void SkipData() {
            Skip(ReadInt32() * 4);
        }

        public AcdEntry ReadEntry() {
            return new AcdEntry {
                Name = ReadString(),
                Data = ReadData()
            };
        }

        [CanBeNull]
        public byte[] ReadEntryData(string entryName) {
            while (BaseStream.Position < BaseStream.Length) {
                var name = ReadString();
                if (string.Equals(name, entryName, StringComparison.OrdinalIgnoreCase)) {
                    return ReadData();
                } else {
                    SkipData();
                }
            }
            return null;
        }
    }
}
