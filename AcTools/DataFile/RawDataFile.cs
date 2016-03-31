using AcTools.AcdFile;

namespace AcTools.DataFile {
    public class RawDataFile : AbstractDataFile {
        public RawDataFile(string carDir, string filename, Acd loadedAcd) : base(carDir, filename, loadedAcd) {}
        public RawDataFile(string carDir, string filename) : base(carDir, filename) {}
        public RawDataFile(string filename) : base(filename) {}
        public RawDataFile() {}

        public string Content { get; private set; }

        protected override void ParseString(string file) {
            Content = file;
        }

        public override void Clear() {
            Content = string.Empty;
        }

        public override string Stringify() {
            return Content;
        }
    }
}