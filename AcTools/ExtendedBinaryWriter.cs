using System.IO;
using System.Text;
using SystemHalf;

namespace AcTools {
    public class ExtendedBinaryWriter : BinaryWriter {
        public ExtendedBinaryWriter(string filename) : base(File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {}
        public ExtendedBinaryWriter(Stream stream) : base(stream) {}

        public void Write(float[] values) {
            for (var i = 0; i < values.Length; i++) {
                Write(values[i]);
            }
        }

        public void WriteHalf(float value) {
            Write(new Half(value).Value);
        }

        public override void Write(string value) {
            var bytes = Encoding.UTF8.GetBytes(value);
            Write(bytes.Length);
            Write(bytes);
        }
    }
}