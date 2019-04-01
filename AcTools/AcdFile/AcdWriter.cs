using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.AcdFile {
    internal class AcdWriter : BinaryWriter {
        [NotNull]
        private readonly IAcdEncryption _enc;

        public AcdWriter([NotNull] string filename) : this(filename, File.Open(filename, FileMode.Create, FileAccess.Write)) {
        }

        public AcdWriter([NotNull] string filename, [NotNull] Stream output) : base(output) {
            _enc = AcdEncryption.FromAcdFilename(filename);
        }

        public override void Write(string value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Write(value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }

        public void Write([NotNull] AcdEntry entry) {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            Write(entry.Name);
            Write(entry.Data.Length);

            var result = new byte[entry.Data.Length * 4];
            _enc.Encrypt(entry.Data, result);
            Write(result);
        }
    }
}
