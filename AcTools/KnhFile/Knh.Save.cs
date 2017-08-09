using AcTools.Utils;

namespace AcTools.KnhFile {
    public partial class Knh {
        public void Save(string filename) {
            using (var writer = new KnhWriter(filename)) {
                writer.Write(RootEntry);
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