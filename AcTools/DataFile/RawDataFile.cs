using AcTools.AcdFile;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public class RawDataFile : DataFileBase {
        public RawDataFile(string filename) : base(filename) {}
        public RawDataFile() {}

        private string _content;

        [NotNull]
        public string Content {
            get => _content ?? "";
            set => _content = value;
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