using System;
using System.IO;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Textures {
    public class RenderableTexture : IRenderableTexture {
        public string Name { get; }

        public bool IsDisposed { get; private set; }

        public RenderableTexture(string name = null) {
            Name = name;
        }

        private ShaderResourceView _resource;

        public ShaderResourceView Resource {
            get { return _override ?? _resource; }
            internal set {
                if (Equals(_resource, value)) return;
                DisposeHelper.Dispose(ref _resource);
                _resource = value;
            }
        }

        private ShaderResourceView _override;

        public ShaderResourceView Override {
            get { return _override; }
            internal set {
                if (Equals(_override, value)) return;
                DisposeHelper.Dispose(ref _override);
                _override = value;
            }
        }

        private int _resourceId, _overrideId;

        public async Task LoadAsync(string filename, Device device) {
            var id = ++_resourceId;
            var resource = await Task.Run(() => ShaderResourceView.FromFile(device, filename));
            if (id != _resourceId) return;
            Resource = resource;
        }

        public async Task LoadAsync(byte[] data, Device device) {
            var id = ++_resourceId;
            var resource = await Task.Run(() => ShaderResourceView.FromMemory(device, data));
            if (id != _resourceId) return;
            Resource = resource;
        }

        public async Task LoadOverrideAsync(string filename, Device device) {
            if (!File.Exists(filename)) {
                Override = null;
                return;
            }

            var id = ++_overrideId;

            try {
                var resource = await Task.Run(() => ShaderResourceView.FromFile(device, filename));
                if (id != _overrideId) return;
                Override = resource;
            } catch (Exception) {
                if (id != _overrideId) return;
                Override = null;
            }
        }

        public void LoadOverride(byte[] data, Device device) {
            var id = ++_overrideId;

            try {
                var resource = ShaderResourceView.FromMemory(device, data);
                if (id != _overrideId) return;
                Override = resource;
            } catch (Exception) {
                if (id != _overrideId) return;
                Override = null;
            }
        }

        public async Task LoadOverrideAsync(byte[] data, Device device) {
            var id = ++_overrideId;

            try {
                var resource = await Task.Run(() => ShaderResourceView.FromMemory(device, data));
                if (id != _overrideId) return;
                Override = resource;
            } catch (Exception) {
                if (id != _overrideId) return;
                Override = null;
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _override);
            DisposeHelper.Dispose(ref _resource);
            IsDisposed = true;
        }
    }
}
