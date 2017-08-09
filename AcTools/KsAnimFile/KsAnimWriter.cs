using System.Collections.Generic;
using System.IO;

namespace AcTools.KsAnimFile {
    internal sealed class KsAnimWriter : ExtendedBinaryWriter {
        private readonly int _version;

        public KsAnimWriter(int version, string filename) : base(filename) {
            _version = version;
            Write(_version);
        }

        public KsAnimWriter(int version, Stream stream) : base(stream) {
            _version = version;
            Write(_version);
        }

        public void Write(int count, IEnumerable<KsAnimEntryBase> entries) {
            Write(count);

            if (_version == 1) {
                foreach (var entry in entries) {
                    Write((KsAnimEntryV1)entry);
                }
            } else {
                foreach (var entry in entries) {
                    Write((KsAnimEntryV2)entry);
                }
            }
        }

        private void Write(KsAnimEntryV1 e) {
            Write(e.NodeName);
            Write(e.Matrices.Length);
            for (var i = 0; i < e.Matrices.Length; i++) {
                Write(e.Matrices[i]);
            }
        }

        private void Write(KsAnimEntryV2 e) {
            Write(e.NodeName);
            Write(e.KeyFrames.Length);
            for (var i = 0; i < e.KeyFrames.Length; i++) {
                Write(e.KeyFrames[i].Rotation);
                Write(e.KeyFrames[i].Transition);
                Write(e.KeyFrames[i].Scale);
            }
        }
    }
}