using System;
using System.IO;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5TextureProvider {
        void GetTexture([NotNull] string textureName, Func<int, Stream> writer);
    }
}