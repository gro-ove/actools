using System.Collections.Generic;

namespace AcTools.KsAnimFile {
    public partial class KsAnim {
        public string OriginalFilename { get; }

        private KsAnim() {
            OriginalFilename = string.Empty;
            Header = new KsAnimHeader { Version = CommonAcConsts.KsAnimActualVersion };
            Entries = new Dictionary<string, KsAnimEntryBase>();
        }

        private KsAnim(string filename) {
            OriginalFilename = filename;
        }

        public KsAnimHeader Header;
        public Dictionary<string, KsAnimEntryBase> Entries;
    }
}
