using System;
using System.Collections.Generic;
using AcTools.Render.Base;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public abstract class TexturesProviderBase : ITexturesProvider {
        protected readonly Dictionary<string, IRenderableTexture> Textures = new Dictionary<string, IRenderableTexture>();

        [NotNull]
        protected abstract IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key);

        [NotNull]
        public IRenderableTexture GetTexture(IDeviceContextHolder contextHolder, string key) {
            IRenderableTexture texture;
            if (Textures.TryGetValue(key, out texture)) return texture;
            return Textures[key] = CreateTexture(contextHolder, key);
        }

        [CanBeNull]
        public IRenderableTexture GetExistingTexture(string key) {
            return Textures.TryGetValue(key, out IRenderableTexture texture) ? texture : null;
        }

        public IEnumerable<IRenderableTexture> GetExistingTextures() {
            return Textures.Values;
        }

        public virtual void Dispose() {
            Textures.DisposeEverything();
        }
    }
}