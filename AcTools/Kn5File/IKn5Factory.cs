using System.IO;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5Factory {
        [NotNull]
        IKn5 FromFile([NotNull] string filename, [CanBeNull] IKn5TextureLoader textureLoader, [CanBeNull] IKn5MaterialLoader materialLoader,
                [CanBeNull] IKn5NodeLoader nodeLoader);

        [NotNull]
        IKn5 FromStream([NotNull] Stream entry, [CanBeNull] IKn5TextureLoader textureLoader, [CanBeNull] IKn5MaterialLoader materialLoader,
                [CanBeNull] IKn5NodeLoader nodeLoader);

        [NotNull]
        IKn5 FromBytes([NotNull] byte[] data, [CanBeNull] IKn5TextureLoader textureLoader);

        [NotNull]
        IKn5 CreateEmpty();
    }
}