using System;
using System.Collections.Generic;
using AcTools.Render.Base;
using AcTools.Utils.Helpers;

namespace AcTools.Render.Kn5Specific.Textures {
    public abstract class TexturesProviderBase : ITexturesProvider {
        public static bool OptionOverrideAsync = false;

        protected readonly Dictionary<string, IRenderableTexture> Textures = new Dictionary<string, IRenderableTexture>();

        protected abstract IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key);

        public IRenderableTexture GetTexture(IDeviceContextHolder contextHolder, string key) {
            IRenderableTexture texture;
            if (Textures.TryGetValue(key, out texture)) return texture;
            return Textures[key] = CreateTexture(contextHolder, key);
        }

        public IRenderableTexture GetExistingTexture(string key) {
            IRenderableTexture texture;
            return Textures.TryGetValue(key, out texture) ? texture : null;
        }

        public IEnumerable<IRenderableTexture> GetExistingTextures() {
            return Textures.Values;
        }

        public virtual void Dispose() {
            Textures.DisposeEverything();
        }
    }
}