using System.IO;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public static class Kn5 {
        public static bool OptionJoinToMultiMaterial = false;
        public static string FbxConverterLocation;

        public static IKn5Factory Factory { get; set; } = Kn5Basic.GetFactoryInstance();

        [NotNull]
        public static IKn5 FromFile(string filename, IKn5TextureLoader textureLoader = null, IKn5MaterialLoader materialLoader = null,
                IKn5NodeLoader nodeLoader = null) {
            return Factory.FromFile(filename, textureLoader, materialLoader, nodeLoader);
        }

        [NotNull]
        public static IKn5 FromStream(Stream entry, IKn5TextureLoader textureLoader = null, IKn5MaterialLoader materialLoader = null,
                IKn5NodeLoader nodeLoader = null) {
            return Factory.FromStream(entry, textureLoader, materialLoader, nodeLoader);
        }

        [NotNull]
        public static IKn5 FromBytes(byte[] data, IKn5TextureLoader textureLoader = null) {
            return Factory.FromBytes(data, textureLoader);
        }

        [NotNull]
        public static IKn5 CreateEmpty() {
            return Factory.CreateEmpty();
        }
    }
}