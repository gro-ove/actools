using System.IO;

namespace AcTools.KsAnimFile {
    internal sealed class KsAnimReader : ReadAheadBinaryReader {
        public KsAnimReader(string filename) : base(filename) {}

        public KsAnimReader(Stream stream) : base(stream) {}

        public KsAnimHeader ReadHeader() {
            var header = new KsAnimHeader {
                Version = ReadInt32()
            };
            
            return header;
        }

        public KsAnimEntryV1 ReadEntryV1() {
            var entry = new KsAnimEntryV1 {
                NodeName = ReadString()
            };

            var keyFramesCount = ReadInt32();
            var matrices = new float[keyFramesCount][];
            for (var i = 0; i < keyFramesCount; i++) {
                matrices[i] = ReadMatrix();
            }

            entry.Matrices = matrices;
            return entry;
        }

        public KsAnimEntryV2 ReadEntryV2() {
            var entry = new KsAnimEntryV2 {
                NodeName = ReadString()
            };

            var keyFramesCount = ReadInt32();
            var keyFrames = new KsAnimKeyframe[keyFramesCount];
            for (var i = 0; i < keyFramesCount; i++) {
                keyFrames[i] = new KsAnimKeyframe(ReadSingle4D(), ReadSingle3D(), ReadSingle3D());
            }

            entry.KeyFrames = keyFrames;
            return entry;
        }
    }
}
