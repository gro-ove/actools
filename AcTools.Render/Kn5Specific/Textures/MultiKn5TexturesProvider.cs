using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class MultiKn5TexturesProvider : TexturesProviderBase {
        public readonly List<IKn5> Kn5;
        protected readonly bool AsyncLoading;

        public MultiKn5TexturesProvider([NotNull] IEnumerable<IKn5> kn5, bool asyncLoading) {
            Kn5 = kn5.ToList();
            AsyncLoading = asyncLoading;
        }

        protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
            var result = new RenderableTexture(key) { Resource = null };

            foreach (var kn5 in Kn5) {
                if (kn5.TexturesData.TryGetValue(key, out var data)) {
                    result.Exists = true;
                    if (AsyncLoading) {
                        result.LoadAsync(contextHolder, data).ContinueWith(t => {
                            contextHolder.RaiseTexturesUpdated();
                        });
                    } else {
                        result.Load(contextHolder, data);
                    }
                    return result;
                }
            }

            return result;
        }
    }
}