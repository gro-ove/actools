using System.ComponentModel;
using System.Linq;

namespace AcTools.Kn5File {
    public class Kn5Material {
        public string Name { get; set; }

        public string ShaderName { get; set; }
        
        public Kn5MaterialBlendMode BlendMode { get; set; }

        public bool AlphaTested { get; set; }

        public Kn5MaterialDepthMode DepthMode { get; set; }

        public ShaderProperty[] ShaderProperties { get; set; }

        public TextureMapping[] TextureMappings { get; set; }

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
        [Description("Opaque")]
        Opaque = 0,

        [Description("Alpha Blend")]
        AlphaBlend = 1,

        [Description("Alpha To Coverage")]
        AlphaToCoverage = 2
    }

    public enum Kn5MaterialDepthMode {
        [Description("Normal")]
        DepthNormal = 0,

        [Description("Read Only")]
        DepthNoWrite = 1,

        [Description("Off")]
        DepthOff = 2
    }
}
