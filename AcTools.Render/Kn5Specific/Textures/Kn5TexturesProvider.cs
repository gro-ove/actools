using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class Kn5TexturesProvider : TexturesProviderBase {
        protected readonly Kn5 Kn5;
        protected readonly bool AsyncLoading;

        public Kn5TexturesProvider([NotNull] Kn5 kn5, bool asyncLoading) {
            Kn5 = kn5;
            AsyncLoading = asyncLoading;
        }

        protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
            var result = new RenderableTexture(key) { Resource = null };

            byte[] data;
            if (Kn5.TexturesData.TryGetValue(key, out data)) {
                if (AsyncLoading) {
                    result.LoadAsync(contextHolder.Device, data).ContinueWith(t => {
                        contextHolder.RaiseTexturesUpdated();
                    });
                } else {
                    result.Load(contextHolder.Device, data);
                }
            }

            return result;
        }
    }
}