using System.IO;
using System.Text;

namespace AcTools.AcdFile {
    internal class AcdWriter : BinaryWriter {
        private readonly AcdEncryption _enc;

        public AcdWriter(string filename) : this(File.Open(filename, FileMode.CreateNew)) {
            _enc = AcdEncryption.FromAcdFilename(filename);
        }

        public AcdWriter(Stream output) : base(output) {}

        public override void Write(string value) {
            Write(value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }

        public void Write(AcdEntry entry) {
            Write(entry.Name);
            Write(entry.Data.Length);

            var copy = new byte[entry.Data.Length];
            entry.Data.CopyTo(copy, 0);
            _enc.Encrypt(copy);
            Write(copy);
        }
    }
}
