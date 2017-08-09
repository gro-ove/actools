using System.IO;

namespace AcTools.KnhFile {
    internal sealed class KnhWriter : ExtendedBinaryWriter {
        public KnhWriter(string filename) : base(filename) {}
        public KnhWriter(Stream output) : base(output) {}

        public void Write(KnhEntry node) {
            Write(node.Name);
            Write(node.Transformation);
            Write(node.Children.Length);
            for (var i = 0; i < node.Children.Length; i++) {
                Write(node.Children[i]);
            }
        }
    }
}