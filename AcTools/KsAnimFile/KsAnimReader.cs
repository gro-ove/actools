using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;

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

        public KsAnimEntry ReadEntry() {
            var entry = new KsAnimEntry {
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
