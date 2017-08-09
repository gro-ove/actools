using AcTools.Utils;

namespace AcTools.KsAnimFile {
    public partial class KsAnim {
        public void Save(string filename) {
            using (var writer = new KsAnimWriter(Header.Version, filename)) {
                writer.Write(Entries.Count, Entries.Values);
            }
        }

        public void SaveRecyclingOriginal(string filename) {
            using (var f = FileUtils.RecycleOriginal(filename)) {
                try {
                    Save(f.Filename);
                } catch {
                    FileUtils.TryToDelete(f.Filename);
                    throw;
                }
            }
        }
    }
}