using System.IO;
using System.Text;

namespace AcTools {
    public class ExtendedBinaryWriter : BinaryWriter {
        public ExtendedBinaryWriter(string filename) : base(File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {}
        public ExtendedBinaryWriter(Stream stream) : base(stream) {}

        public void Write(float[] values) {
            for (var i = 0; i < values.Length; i++) {
                Write(values[i]);
            }
        }

        public override void Write(string value) {
            Write(value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }
    }
}