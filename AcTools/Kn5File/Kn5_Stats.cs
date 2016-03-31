using System.Linq;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public int NodesCount {
            get { return Nodes.Count(); }
        }

        public bool IsWithoutTextures() {
            return Textures.Count == 0;
        }
    }
}
