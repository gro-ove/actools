using System.Collections.Generic;
using System.IO;

namespace AcTools.KsAnimFile {
    public partial class KsAnim {
        public static KsAnim FromFile(string filename) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }

            var kn5 = new KsAnim(filename);

            using (var reader = new KsAnimReader(filename)) {
                kn5.FromFile_Header(reader);
                kn5.FromFile_Nodes(reader);
            }

            return kn5;
        }

        private void FromFile_Header(KsAnimReader reader) {
            Header = reader.ReadHeader();
        }

        private void FromFile_Nodes(KsAnimReader reader) {
            var count = reader.ReadInt32();
            Entries = new Dictionary<string, KsAnimEntryBase>(count);

            for (var i = 0; i < count; i++) {
                if (Header.Version == 2) {
                    var entry = reader.ReadEntryV2();
                    if (entry.KeyFrames.Length > 0) {
                        Entries[entry.NodeName] = entry;
                    }
                } else {
                    var entry = reader.ReadEntryV1();
                    if (entry.Matrices.Length > 0) {
                        Entries[entry.NodeName] = entry;
                    }
                }
            }
        }
    }
}
