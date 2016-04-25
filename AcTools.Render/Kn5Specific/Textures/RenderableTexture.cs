using AcTools.Utils.Helpers;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Textures {
    public class RenderableTexture : IRenderableTexture {
        private ShaderResourceView _resource;

        public ShaderResourceView Resource {
            get { return _resource; }
            internal set {
                if (Equals(_resource, value)) return;
                DisposeHelper.Dispose(ref _resource);
                _resource = value;
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _resource);
        }
    }
}
