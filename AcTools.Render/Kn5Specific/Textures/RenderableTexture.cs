using AcTools.Render.Base.Utils;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Textures {
    public class RenderableTexture : IRenderableTexture {
        private ShaderResourceView _resource;

        public ShaderResourceView Resource {
            get { return _resource; }
            internal set {
                if (Equals(_resource, value)) return;
                SlimDxExtension.Dispose(ref _resource);
                _resource = value;
            }
        }

        public void Dispose() {
            SlimDxExtension.Dispose(ref _resource);
        }
    }
}
