using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.AcdFile {
    internal sealed class AcdReader : BinaryReader {
        [NotNull]
        private readonly AcdEncryption _enc;

        public AcdReader(string filename) : this(filename, File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {}

        public AcdReader(string filename, Stream input) : base(input) {
            _enc = AcdEncryption.FromAcdFilename(filename);

            if (ReadInt32() == -1111){
                ReadInt32();
            } else {
                BaseStream.Seek(0, SeekOrigin.Begin);
            }
        }

        public override string ReadString() {
            var length = ReadInt32();
            if (length < 0) {
                throw new Exception("Damaged file");
            }

            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        private byte[] ReadData() {
            var length = ReadInt32();
            var result = new byte[length];
            for (var i = 0; i < length; i++) {
                result[i] = ReadByte();
                BaseStream.Seek(3, SeekOrigin.Current);
            }
            
            _enc.Decrypt(result);
            return result;
        }

        private void SkipData() {
            var length = ReadInt32();
            BaseStream.Seek(length * 4, SeekOrigin.Current);
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
