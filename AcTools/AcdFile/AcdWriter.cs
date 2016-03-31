using System.IO;
using System.Text;

namespace AcTools.AcdFile {
    internal class AcdWriter : BinaryWriter {
        private readonly AcdEncryption _enc;

        public AcdWriter(string filename)
            : this(File.Open(filename, FileMode.CreateNew)) {
            _enc = AcdEncryption.FromAcdFilename(filename);
        }

        public AcdWriter(Stream output)
            : base(output) {
        }

        override public void Write(string value) {
            Write(value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }

        public void Write(AcdEntry entry) {
            Write(entry.Name);
            Write(entry.Data.Length);

            for (var i = 0; i < entry.Data.Length; i++) {
                Write((uint)_enc.Encrypt(entry.Data[i], i));
            }
        }
    }
}
