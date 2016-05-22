using System.IO;
using System.Text;

namespace AcTools.AcdFile {
    internal sealed class AcdReader : BinaryReader {
        private readonly AcdEncryption _enc;

        public AcdReader(string filename) : this(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            _enc = AcdEncryption.FromAcdFilename(filename);
        }

        public AcdReader(Stream input) : base(input) {
            if (ReadInt32() == -1111){
                ReadInt32();
            } else {
                BaseStream.Seek(0, SeekOrigin.Begin);
            }
        }

        public override string ReadString() {
            var length = ReadInt32();
            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        public byte[] ReadData() {
            var length = ReadInt32();
            var result = new byte[length];
            for (var i = 0; i < length; i++) {
                result[i] = ReadByte();
                BaseStream.Seek(3, SeekOrigin.Current);
            }
            
            _enc.Decrypt(result);
            return result;
        }

        public AcdEntry ReadEntry() {
            return new AcdEntry {
                Name = ReadString(),
                Data = ReadData()
            };
        }
    }
}
