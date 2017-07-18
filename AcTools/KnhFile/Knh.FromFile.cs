using System.IO;

namespace AcTools.KnhFile {
    public partial class Knh {
        public static Knh FromFile(string filename) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException(filename);
            }
            
            using (var reader = new KnhReader(filename)) {
                return new Knh(filename, reader.ReadEntry());
            }
        }
    }
}
