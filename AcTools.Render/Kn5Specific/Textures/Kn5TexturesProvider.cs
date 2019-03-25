using AcTools.Kn5File;
using AcTools.Render.Base;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class Kn5TexturesProvider : TexturesProviderBase {
        protected readonly IKn5 Kn5;
        protected readonly bool AsyncLoading;

        public Kn5TexturesProvider([NotNull] IKn5 kn5, bool asyncLoading) {
            Kn5 = kn5;
            AsyncLoading = asyncLoading;
        }

        protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
            var result = new RenderableTexture(key) { Resource = null };

            if (Kn5.TexturesData.TryGetValue(key, out var data)) {
                result.Exists = true;
                if (AsyncLoading) {
                    result.LoadAsync(contextHolder, data).ContinueWith(t => {
                        contextHolder.RaiseTexturesUpdated();
                    });
                } else {
                    result.Load(contextHolder, data);
                }
            }

            return result;
        }
    }
}