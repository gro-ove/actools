using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class Kn5TexturesProvider : TexturesProviderBase {
        protected readonly Kn5 _kn5;

        public Kn5TexturesProvider([NotNull] Kn5 kn5) {
            _kn5 = kn5;
        }

        protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
            var result = new RenderableTexture(key) { Resource = null };

            byte[] data;
            if (_kn5.TexturesData.TryGetValue(key, out data)) {
                result.LoadAsync(contextHolder.Device, data).Forget();
            }

            return result;
        }
    }
}