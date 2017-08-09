using System.IO;

namespace AcTools.KnhFile {
    internal sealed class KnhReader : ReadAheadBinaryReader {
        public KnhReader(string filename) : base(filename) {}
        public KnhReader(Stream stream) : base(stream) {}

        public KnhEntry ReadEntry() {
            var name = ReadString();
            var transform = ReadMatrix();
            var children = new KnhEntry[ReadInt32()];
            for (var i = 0; i < children.Length; i++) {
                children[i] = ReadEntry();
            }

            return new KnhEntry(name, transform, children);
        }
    }
}
