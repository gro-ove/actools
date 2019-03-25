using System.IO;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5Factory {
        [NotNull]
        IKn5 FromFile(string filename, [CanBeNull] IKn5TextureLoader textureLoader, [CanBeNull] IKn5MaterialLoader materialLoader,
                [CanBeNull] IKn5NodeLoader nodeLoader);

        [NotNull]
        IKn5 FromStream(Stream entry, [CanBeNull] IKn5TextureLoader textureLoader, [CanBeNull] IKn5MaterialLoader materialLoader,
                [CanBeNull] IKn5NodeLoader nodeLoader);

        [NotNull]
        IKn5 FromBytes(byte[] data, [CanBeNull] IKn5TextureLoader textureLoader);

        [NotNull]
        IKn5 CreateEmpty();
    }
}