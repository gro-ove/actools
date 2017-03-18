using System;
using System.IO;
using System.Threading.Tasks;
using AcTools.Render.Base;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX.Direct3D11;
using Debug = System.Diagnostics.Debug;

namespace AcTools.Render.Kn5Specific.Textures {
    public class RenderableTexture : IRenderableTexture {
        public string Name { get; }

        public bool IsDisposed { get; private set; }

        public RenderableTexture(string name = null) {
            Name = name;
            Debug.WriteLine("[RenderableTexture] CREATED: " + name);
        }

        private ShaderResourceView _resource;

        public ShaderResourceView Resource {
            get { return _proceduralOverride ?? _override ?? _resource; }
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

        private ShaderResourceView _proceduralOverride;

        public ShaderResourceView ProceduralOverride {
            get { return _proceduralOverride; }
            set {
                if (Equals(value, _proceduralOverride)) return;
                DisposeHelper.Dispose(ref _proceduralOverride);
                _proceduralOverride = value;
            }
        }

        [CanBeNull]
        private static ShaderResourceView LoadSafe([CanBeNull] Device device, byte[] bytes) {
            if (device == null) return null;
            try {
                return ShaderResourceView.FromMemory(device, bytes);
            } catch (Exception) {
                AcToolsLogging.Write("Texture damaged");
                return null;
            }
        }

        [CanBeNull]
        private static ShaderResourceView LoadSafe(Device device, string filename) {
            if (device == null) return null;
            try {
                return ShaderResourceView.FromFile(device, filename);
            } catch (Exception) {
                AcToolsLogging.Write("Texture damaged");
                return null;
            }
        }

        public void SetProceduralOverride(IDeviceContextHolder holder, byte[] textureBytes) {
            if (textureBytes == null) {
                ProceduralOverride = null;
                holder?.RaiseTexturesUpdated();
                return;
            }

            try {
                ProceduralOverride = LoadSafe(holder?.Device, textureBytes);
                holder?.RaiseTexturesUpdated();
            } catch (Exception) {
                ProceduralOverride = null;
                holder?.RaiseTexturesUpdated();
            }
        }

        private int _resourceId, _overrideId;

        public void Load([CanBeNull] IDeviceContextHolder holder, string filename) {
            var id = ++_resourceId;
            var resource = LoadSafe(holder?.Device, filename);
            if (id != _resourceId) return;
            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public void Load([CanBeNull] IDeviceContextHolder holder, byte[] data) {
            var id = ++_resourceId;
            var resource = LoadSafe(holder?.Device, data);
            if (id != _resourceId) return;
            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public async Task LoadAsync([CanBeNull] IDeviceContextHolder holder, string filename) {
            var id = ++_resourceId;
            var resource = await Task.Run(() => LoadSafe(holder?.Device, filename));
            if (id != _resourceId) return;
            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public async Task LoadAsync([CanBeNull] IDeviceContextHolder holder, byte[] data) {
            var id = ++_resourceId;
            var resource = await Task.Run(() => LoadSafe(holder?.Device, data));
            if (id != _resourceId) return;
            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public async Task LoadOverrideAsync([CanBeNull] IDeviceContextHolder holder, string filename) {
            var id = ++_overrideId;

            if (filename == null || !File.Exists(filename)) {
                Override = null;
                holder?.RaiseTexturesUpdated();
                return;
            }

            try {
                var resource = await Task.Run(() => LoadSafe(holder?.Device, filename));
                if (id != _overrideId) return;
                Override = resource;
                holder?.RaiseTexturesUpdated();
            } catch (Exception) {
                if (id != _overrideId) return;
                Override = null;
                holder?.RaiseTexturesUpdated();
            }
        }

        public void LoadOverride([CanBeNull] IDeviceContextHolder holder, byte[] data) {
            var id = ++_overrideId;

            if (data == null) {
                Override = null;
                holder?.RaiseTexturesUpdated();
                return;
            }

            try {
                var resource = LoadSafe(holder?.Device, data);
                if (id != _overrideId) return;
                Override = resource;
                holder?.RaiseTexturesUpdated();
            } catch (Exception) {
                if (id != _overrideId) return;
                Override = null;
                holder?.RaiseTexturesUpdated();
            }
        }

        public async Task LoadOverrideAsync([NotNull] IDeviceContextHolder holder, [CanBeNull] byte[] data) {
            var id = ++_overrideId;

            if (data == null) {
                Override = null;
                holder.RaiseTexturesUpdated();
                return;
            }

            try {
                var resource = await Task.Run(() => LoadSafe(holder.Device, data));
                if (id != _overrideId) return;
                Override = resource;
                holder.RaiseTexturesUpdated();
            } catch (Exception) {
                if (id != _overrideId) return;
                Override = null;
                holder.RaiseTexturesUpdated();
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _override);
            DisposeHelper.Dispose(ref _resource);
            Debug.WriteLine("[RenderableTexture] DISPOSED: " + Name);
            IsDisposed = true;
        }
    }
}
