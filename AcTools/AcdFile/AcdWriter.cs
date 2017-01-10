using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.AcdFile {
    internal class AcdWriter : BinaryWriter {
        [NotNull]
        private readonly AcdEncryption _enc;

        public AcdWriter(string filename) : this(filename, File.Open(filename, FileMode.CreateNew)) {}

        public AcdWriter(string filename, Stream output) : base(output) {
            _enc = AcdEncryption.FromAcdFilename(filename);
        }

        public override void Write(string value) {
            Write(value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }

        public void Write(AcdEntry entry) {
            Write(entry.Name);
            Write(entry.Data.Length);

            var result = new byte[entry.Data.Length * 4];
            _enc.Encrypt(entry.Data, result);
            Write(result);
        }
    }
}
