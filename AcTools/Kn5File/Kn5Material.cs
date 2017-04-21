using System.ComponentModel;
using JetBrains.Annotations;

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

        [CanBeNull]
        public ShaderProperty GetPropertyByName([Localizable(false)] string name) {
            for (var i = 0; i < ShaderProperties.Length; i++) {
                var t = ShaderProperties[i];
                if (t.Name == name) return t;
            }

            return null;
        }

        public class TextureMapping {
            public string Name, Texture;
            public int Slot;
        }

        [CanBeNull]
        public TextureMapping GetMappingByName(string name) {
            for (int i = 0; i < TextureMappings.Length; i++) {
                var t = TextureMappings[i];
                if (t.Name == name) return t;
            }

            return null;
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

    internal static class Kn5MaterialExtension {
        public static bool IsValidDepthMode(this int v) {
            return v >= 0 && v <= 2;
        }

        public static bool IsValidBlendMode(this byte v) {
            return v <= 2;
        }
    }
}
