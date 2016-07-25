using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public static Kn5 CreateEmpty() {
            return new Kn5 {
                RootNode = Kn5Node.CreateBaseNode("Root")
            };
        }

        public void SetTexture([Localizable(false)] string textureName, string filename) {
            var bytes = File.ReadAllBytes(filename);
            Textures[textureName] = new Kn5Texture {
                Active = true,
                Name = textureName,
                Length = bytes.Length
            };
            TexturesData[textureName] = bytes;
        }
    }
}
