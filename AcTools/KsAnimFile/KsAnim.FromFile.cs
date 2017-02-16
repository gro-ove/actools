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
            Entries = new Dictionary<string, KsAnimEntry>(count);

            for (var i = 0; i < count; i++) {
                var entry = reader.ReadEntry();
                if (entry.KeyFrames.Length > 0) {
                    Entries[entry.NodeName] = entry;
                }
            }
        }
    }
}
