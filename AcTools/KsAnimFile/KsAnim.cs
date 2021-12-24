using System.Collections.Generic;
using System.Linq;

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

        public static KsAnim FromEntries(IEnumerable<KsAnimEntryV1> entries) {
            return new KsAnim {
                Header = new KsAnimHeader { Version = 1 },
                Entries = entries.ToDictionary(x => x.NodeName, x => (KsAnimEntryBase)x)
            };
        }

        public static KsAnim FromEntries(IEnumerable<KsAnimEntryV2> entries) {
            return new KsAnim {
                Header = new KsAnimHeader { Version = 2 },
                Entries = entries.ToDictionary(x => x.NodeName, x => (KsAnimEntryBase)x)
            };
        }
    }
}
