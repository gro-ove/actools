using System.Linq;

namespace AcTools.Kn5File {
    public class Kn5Material {
        public string Name, ShaderName;
        public Kn5MaterialBlendMode BlendMode;
        public bool AlphaTested;
        public Kn5MaterialDepthMode DepthMode;
        public ShaderProperty[] ShaderProperties;
        public TextureMapping[] TextureMappings;

        public class ShaderProperty {
            public string Name;
            public float ValueA;
            public float[] ValueB;
            public float[] ValueC;
            public float[] ValueD;
        }

        public ShaderProperty GetPropertyByName(string name) {
            return ShaderProperties.FirstOrDefault(t => t.Name == name);
        }

        public class TextureMapping {
            public string Name, Texture;
            public int Slot;
        }

        public TextureMapping GetMappingByName(string name) {
            return TextureMappings.FirstOrDefault(t => t.Name == name);
        }
    }

    public enum Kn5MaterialBlendMode {
        Opaque = 0,
        AlphaBlend = 1,
        AlphaToCoverage = 2
    }

    public enum Kn5MaterialDepthMode {
        DepthNormal = 0,
        DepthNoWrite = 1,
        DepthOff = 2
    }
}
