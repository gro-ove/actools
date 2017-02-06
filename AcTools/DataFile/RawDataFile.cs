using AcTools.AcdFile;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class RawDataFile : AbstractDataFile {
        public RawDataFile(string carDir, string filename, Acd loadedAcd) : base(carDir, filename, loadedAcd) {}

        public RawDataFile(string carDir, string filename) : base(carDir, filename) {}

        public RawDataFile(string filename) : base(filename) {}

        public RawDataFile() {}

        private string _content;

        [NotNull]
        public string Content {
            get { return _content ?? ""; }
            set { _content = value; }
        }

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